using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipNet.Interop;

namespace SevenZipNet;

/// <summary>Metadata about a supported archive format.</summary>
public record ArchiveFormat(string Name, string Extension, Guid ClassId, bool CanUpdate);

/// <summary>
/// Loads 7-Zip's native DLL, resolves exports, and provides factory methods
/// for creating COM archive objects.
/// </summary>
public sealed class SevenZipLibrary : IDisposable
{
    private readonly nint _lib;
    private readonly CreateObjectFunc _createObject;
    private readonly StrategyBasedComWrappers _comWrappers = new();
    private bool _disposed;

    public IReadOnlyList<ArchiveFormat> Formats { get; }

    public SevenZipLibrary(string dllPath)
    {
        _lib = NativeLibrary.Load(dllPath);

        _createObject = Marshal.GetDelegateForFunctionPointer<CreateObjectFunc>(
            NativeLibrary.GetExport(_lib, "CreateObject"));
        var getNumberOfFormats = Marshal.GetDelegateForFunctionPointer<GetNumberOfFormatsFunc>(
            NativeLibrary.GetExport(_lib, "GetNumberOfFormats"));
        var getHandlerProperty2 = Marshal.GetDelegateForFunctionPointer<GetHandlerProperty2Func>(
            NativeLibrary.GetExport(_lib, "GetHandlerProperty2"));

        Formats = LoadFormats(getNumberOfFormats, getHandlerProperty2);
    }

    /// <summary>Create an IInArchive COM object for the given format CLSID.</summary>
    public ArchiveHandle CreateInArchive(Guid classId)
    {
        Guid iid = new("23170F69-40C1-278A-0000-000600600000"); // IID_IInArchive
        int hr = _createObject(in classId, in iid, out nint ptr);
        Marshal.ThrowExceptionForHR(hr);
        var archive = (IInArchive)_comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.UniqueInstance);
        return new ArchiveHandle(archive, _comWrappers);
    }

    public StrategyBasedComWrappers ComWrappers => _comWrappers;

    /// <summary>Create a .7z archive from in-memory file data.</summary>
    public void CreateArchive(Guid classId, Stream outputStream, Dictionary<string, byte[]> files)
    {
        Guid iid = new("23170F69-40C1-278A-0000-000600A00000"); // IID_IOutArchive
        int hr = _createObject(in classId, in iid, out nint ptr);
        Marshal.ThrowExceptionForHR(hr);

        var outArchive = (IOutArchive)_comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.UniqueInstance);

        var outWrapper = new OutStreamWrapper(outputStream);
        var updateCallback = new UpdateCallback(files, _comWrappers);

        nint outCcw = _comWrappers.GetOrCreateComInterfaceForObject(outWrapper, CreateComInterfaceFlags.None);
        Guid iidOutStream = new("23170F69-40C1-278A-0000-000300040000"); // IOutStream (seekable)
        Marshal.QueryInterface(outCcw, ref iidOutStream, out nint outPtr);

        nint cbCcw = _comWrappers.GetOrCreateComInterfaceForObject(updateCallback, CreateComInterfaceFlags.None);
        Guid iidUpdateCb = new("23170F69-40C1-278A-0000-000600800000");
        Marshal.QueryInterface(cbCcw, ref iidUpdateCb, out nint cbPtr);

        hr = outArchive.UpdateItems(outPtr, (uint)files.Count, cbPtr);

        if (outPtr != nint.Zero) Marshal.Release(outPtr);
        if (cbPtr != nint.Zero) Marshal.Release(cbPtr);
        Marshal.Release(outCcw);
        Marshal.Release(cbCcw);
        GC.KeepAlive(outWrapper);
        GC.KeepAlive(updateCallback);

        Marshal.ThrowExceptionForHR(hr);
    }

    private static List<ArchiveFormat> LoadFormats(
        GetNumberOfFormatsFunc getNumberOfFormats,
        GetHandlerProperty2Func getHandlerProperty2)
    {
        getNumberOfFormats(out uint count);
        var formats = new List<ArchiveFormat>((int)count);

        for (uint i = 0; i < count; i++)
        {
            PropVariant pv = default;

            getHandlerProperty2(i, HandlerPropId.kName, ref pv);
            string name = pv.GetBstr() ?? "";
            NativeMethods.PropVariantClear(ref pv);

            pv = default;
            getHandlerProperty2(i, HandlerPropId.kExtension, ref pv);
            string ext = pv.GetBstr() ?? "";
            NativeMethods.PropVariantClear(ref pv);

            pv = default;
            getHandlerProperty2(i, HandlerPropId.kClassID, ref pv);
            Guid classId = Guid.Empty;
            if (pv.VarType == PropVariant.VT_BSTR && pv.PointerValue != nint.Zero)
            {
                // ClassID is stored as binary GUID in a BSTR (raw bytes, not string)
                unsafe { classId = *(Guid*)pv.PointerValue; }
            }
            NativeMethods.PropVariantClear(ref pv);

            pv = default;
            getHandlerProperty2(i, HandlerPropId.kUpdate, ref pv);
            bool canUpdate = pv.GetBool();
            NativeMethods.PropVariantClear(ref pv);

            formats.Add(new ArchiveFormat(name, ext, classId, canUpdate));
        }

        return formats;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Don't call NativeLibrary.Free — COM objects may still reference
            // the DLL's vtables. The OS unloads it at process exit.
            _disposed = true;
        }
    }
}

/// <summary>Wrapper around IInArchive with the ComWrappers instance for CCW creation.</summary>
public sealed class ArchiveHandle : IDisposable
{
    public IInArchive Archive { get; }
    public StrategyBasedComWrappers ComWrappers { get; }

    // prevent GC of managed COM wrappers while native code holds references
    private InStreamWrapper? _streamWrapper;
    private ArchiveOpenCallback? _openCallback;

    internal ArchiveHandle(IInArchive archive, StrategyBasedComWrappers comWrappers)
    {
        Archive = archive;
        ComWrappers = comWrappers;
    }

    /// <summary>Open an archive from the given stream.</summary>
    public void Open(Stream stream, ulong maxCheckStartPosition = 1 << 23)
    {
        _streamWrapper = new InStreamWrapper(stream);
        _openCallback = new ArchiveOpenCallback();

        nint streamCcw = ComWrappers.GetOrCreateComInterfaceForObject(_streamWrapper, CreateComInterfaceFlags.None);
        Guid iidInStream = new("23170F69-40C1-278A-0000-000300030000");
        Marshal.QueryInterface(streamCcw, ref iidInStream, out nint streamPtr);

        nint callbackCcw = ComWrappers.GetOrCreateComInterfaceForObject(_openCallback, CreateComInterfaceFlags.None);
        Guid iidCallback = new("23170F69-40C1-278A-0000-000600100000");
        Marshal.QueryInterface(callbackCcw, ref iidCallback, out nint callbackPtr);

        int hr;
        unsafe
        {
            ulong scanSize = maxCheckStartPosition;
            hr = Archive.Open(streamPtr, (nint)(&scanSize), callbackPtr);
        }

        // Release our QI references
        if (streamPtr != nint.Zero) Marshal.Release(streamPtr);
        if (callbackPtr != nint.Zero) Marshal.Release(callbackPtr);
        Marshal.Release(streamCcw);
        Marshal.Release(callbackCcw);

        Marshal.ThrowExceptionForHR(hr);
    }

    public void Dispose()
    {
        Archive.Close();
        _streamWrapper = null;
        _openCallback = null;
    }

    /// <summary>Extract all files to memory. Returns dict of path → byte[].</summary>
    public Dictionary<string, byte[]> ExtractAll()
    {
        var callback = new ExtractCallback(Archive, ComWrappers);
        CallExtract(null, 0, callback);

        // Map index→bytes to path→bytes
        Archive.GetNumberOfItems(out uint count);
        var result = new Dictionary<string, byte[]>();
        for (uint i = 0; i < count; i++)
        {
            if (!callback.ExtractedData.TryGetValue(i, out var data)) continue;
            PropVariant pv = default;
            Archive.GetProperty(i, PropId.kpidPath, ref pv);
            string path = pv.GetBstr() ?? i.ToString();
            NativeMethods.PropVariantClear(ref pv);
            result[path] = data;
        }
        return result;
    }

    /// <summary>Extract specific items by index. Returns dict of index → byte[].</summary>
    public Dictionary<uint, byte[]> Extract(uint[] indices)
    {
        var callback = new ExtractCallback(Archive, ComWrappers);
        CallExtract(indices, 0, callback);
        return callback.ExtractedData;
    }

    /// <summary>Test archive integrity (extract in verify-only mode).</summary>
    public void Test()
    {
        var callback = new ExtractCallback(Archive, ComWrappers);
        // numItems = 0xFFFFFFFF means "all", testMode = 1
        CallExtract(null, 1, callback);
    }

    private void CallExtract(uint[]? indices, int testMode, ExtractCallback callback)
    {
        var cw = ComWrappers;
        nint callbackCcw = cw.GetOrCreateComInterfaceForObject(callback, CreateComInterfaceFlags.None);
        Guid iidExtractCb = new("23170F69-40C1-278A-0000-000600200000");
        Marshal.QueryInterface(callbackCcw, ref iidExtractCb, out nint callbackPtr);

        int hr;
        if (indices == null)
        {
            // Extract all: pass NULL indices and numItems = 0xFFFFFFFF
            hr = Archive.Extract(nint.Zero, 0xFFFFFFFF, testMode, callbackPtr);
        }
        else
        {
            unsafe
            {
                fixed (uint* pIndices = indices)
                {
                    hr = Archive.Extract((nint)pIndices, (uint)indices.Length, testMode, callbackPtr);
                }
            }
        }

        if (callbackPtr != nint.Zero) Marshal.Release(callbackPtr);
        Marshal.Release(callbackCcw);
        GC.KeepAlive(callback);
        Marshal.ThrowExceptionForHR(hr);
    }
}
