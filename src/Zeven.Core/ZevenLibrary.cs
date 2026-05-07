using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core;

/// <summary>Metadata about a supported archive format.</summary>
public record ArchiveFormat(string Name, string Extension, Guid ClassId, bool CanUpdate);

/// <summary>
/// Loads 7-Zip's native DLL, resolves exports, and provides factory methods
/// for creating COM archive objects. Use <see cref="Load"/> to obtain the instance.
/// Only one DLL can be loaded per process.
/// </summary>
public sealed class ZevenLibrary : IDisposable
{
    private static ZevenLibrary? instance;
    private static readonly Lock syncLock = new();

    private readonly nint lib;
    private readonly CreateObjectFunc createObject;
    private readonly CreateEncoderFunc createEncoder;
    private readonly CreateDecoderFunc createDecoder;
    private readonly GetNumberOfMethodsFunc getNumberOfMethods;
    private readonly GetMethodPropertyFunc getMethodProperty;
    private readonly StrategyBasedComWrappers comWrappers = new();
    private bool disposed;

    public IReadOnlyList<ArchiveFormat> Formats { get; }

    private ZevenLibrary(string dllPath)
    {
        this.lib = NativeLibrary.Load(dllPath);

        this.createObject = Marshal.GetDelegateForFunctionPointer<CreateObjectFunc>(
            NativeLibrary.GetExport(this.lib, "CreateObject"));
        var getNumberOfFormats = Marshal.GetDelegateForFunctionPointer<GetNumberOfFormatsFunc>(
            NativeLibrary.GetExport(this.lib, "GetNumberOfFormats"));
        var getHandlerProperty2 = Marshal.GetDelegateForFunctionPointer<GetHandlerProperty2Func>(
            NativeLibrary.GetExport(this.lib, "GetHandlerProperty2"));

        this.createEncoder = Marshal.GetDelegateForFunctionPointer<CreateEncoderFunc>(
            NativeLibrary.GetExport(this.lib, "CreateEncoder"));
        this.createDecoder = Marshal.GetDelegateForFunctionPointer<CreateDecoderFunc>(
            NativeLibrary.GetExport(this.lib, "CreateDecoder"));
        this.getNumberOfMethods = Marshal.GetDelegateForFunctionPointer<GetNumberOfMethodsFunc>(
            NativeLibrary.GetExport(this.lib, "GetNumberOfMethods"));
        this.getMethodProperty = Marshal.GetDelegateForFunctionPointer<GetMethodPropertyFunc>(
            NativeLibrary.GetExport(this.lib, "GetMethodProperty"));

        this.Formats = LoadFormats(getNumberOfFormats, getHandlerProperty2);
    }

    /// <summary>
    /// Load a 7-Zip native DLL and return the library instance.
    /// The DLL is loaded once; subsequent calls return the same instance.
    /// </summary>
    public static ZevenLibrary Load(string dllPath)
    {
        lock (syncLock)
        {
            if (instance != null)
            {
                return instance;
            }
            instance = new ZevenLibrary(Path.GetFullPath(dllPath));
            return instance;
        }
    }

    /// <summary>
    /// Get the already-loaded library instance.
    /// Throws if <see cref="Load"/> has not been called yet.
    /// </summary>
    public static ZevenLibrary Instance
    {
        get
        {
            var inst = instance;
            if (inst == null)
            {
                throw new InvalidOperationException(
                    "ZevenLibrary has not been loaded. Call ZevenLibrary.Load(dllPath) first.");
            }
            return inst;
        }
    }

    /// <summary>Create an IInArchive COM object for the given format CLSID.</summary>
    public ArchiveHandle CreateInArchive(Guid classId)
    {
        Guid iid = Iid.IInArchive;
        int hr = this.createObject(in classId, in iid, out nint ptr);
        Marshal.ThrowExceptionForHR(hr);
        var archive = (IInArchive)this.comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.UniqueInstance);
        return new ArchiveHandle(archive, this.comWrappers);
    }

    /// <summary>Create an IInArchive COM object by format name (e.g., "7z", "Zip", "Tar").</summary>
    public ArchiveHandle CreateInArchive(string formatName)
    {
        return this.CreateInArchive(this.ResolveFormat(formatName));
    }

    public StrategyBasedComWrappers ComWrappers => this.comWrappers;

    /// <summary>Find the index of a codec by its 7-Zip codec ID (e.g., 0x21 for LZMA2).</summary>
    /// <returns>Codec index, or -1 if not found.</returns>
    public int FindCodecIndex(ulong codecId)
    {
        this.getNumberOfMethods(out uint count);
        for (uint i = 0; i < count; i++)
        {
            PropVariant pv = default;
            this.getMethodProperty(i, MethodPropId.Id, ref pv);
            ulong id = pv.GetUInt64();
            if (id == codecId)
            {
                return (int)i;
            }
        }
        return -1;
    }

    /// <summary>Create an encoder COM object for the codec at the given index.</summary>
    public nint CreateEncoderObject(uint index)
    {
        Guid iid = Iid.ICompressCoder;
        int hr = this.createEncoder(index, in iid, out nint ptr);
        Marshal.ThrowExceptionForHR(hr);
        return ptr;
    }

    /// <summary>Create a decoder COM object for the codec at the given index.</summary>
    public nint CreateDecoderObject(uint index)
    {
        Guid iid = Iid.ICompressCoder;
        int hr = this.createDecoder(index, in iid, out nint ptr);
        Marshal.ThrowExceptionForHR(hr);
        return ptr;
    }

    /// <summary>Create a .7z archive from in-memory file data.</summary>
    public void CreateArchive(Guid classId, Stream outputStream, Dictionary<string, byte[]> files, string? password = null)
    {
        Guid iid = Iid.IOutArchive;
        int hr = this.createObject(in classId, in iid, out nint ptr);
        Marshal.ThrowExceptionForHR(hr);

        var outArchive = (IOutArchive)this.comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.UniqueInstance);

        var outWrapper = new OutStreamWrapper(outputStream);
        var updateCallback = new UpdateCallback(files, this.comWrappers, password);

        nint outCcw = this.comWrappers.GetOrCreateComInterfaceForObject(outWrapper, CreateComInterfaceFlags.None);
        Guid iidOutStream = Iid.IOutStream;
        Marshal.QueryInterface(outCcw, ref iidOutStream, out nint outPtr);

        nint cbCcw = this.comWrappers.GetOrCreateComInterfaceForObject(updateCallback, CreateComInterfaceFlags.None);
        Guid iidUpdateCb = Iid.IArchiveUpdateCallback;
        Marshal.QueryInterface(cbCcw, ref iidUpdateCb, out nint cbPtr);

        hr = outArchive.UpdateItems(outPtr, (uint)files.Count, cbPtr);

        if (outPtr != nint.Zero) { Marshal.Release(outPtr); }
        if (cbPtr != nint.Zero) { Marshal.Release(cbPtr); }
        Marshal.Release(outCcw);
        Marshal.Release(cbCcw);
        GC.KeepAlive(outWrapper);
        GC.KeepAlive(updateCallback);

        Marshal.ThrowExceptionForHR(hr);
    }

    /// <summary>Create an archive from files on disk. Files are streamed — not loaded into memory.</summary>
    public void CreateArchive(Guid classId, Stream outputStream,
            Dictionary<string, string> files, string? password = null)
    {
        Guid iid = Iid.IOutArchive;
        int hr = this.createObject(in classId, in iid, out nint ptr);
        Marshal.ThrowExceptionForHR(hr);

        var outArchive = (IOutArchive)this.comWrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.UniqueInstance);

        var outWrapper = new OutStreamWrapper(outputStream);
        var updateCallback = new FileUpdateCallback(files, this.comWrappers, password);

        nint outCcw = this.comWrappers.GetOrCreateComInterfaceForObject(outWrapper, CreateComInterfaceFlags.None);
        Guid iidOutStream = Iid.IOutStream;
        Marshal.QueryInterface(outCcw, ref iidOutStream, out nint outPtr);

        nint cbCcw = this.comWrappers.GetOrCreateComInterfaceForObject(updateCallback, CreateComInterfaceFlags.None);
        Guid iidUpdateCb = Iid.IArchiveUpdateCallback;
        Marshal.QueryInterface(cbCcw, ref iidUpdateCb, out nint cbPtr);

        hr = outArchive.UpdateItems(outPtr, (uint)files.Count, cbPtr);

        if (outPtr != nint.Zero) { Marshal.Release(outPtr); }
        if (cbPtr != nint.Zero) { Marshal.Release(cbPtr); }
        Marshal.Release(outCcw);
        Marshal.Release(cbCcw);
        GC.KeepAlive(outWrapper);
        GC.KeepAlive(updateCallback);

        Marshal.ThrowExceptionForHR(hr);
    }

    /// <summary>Create an archive from in-memory file data by format name (e.g., "7z", "Zip", "Tar").</summary>
    public void CreateArchive(string formatName, Stream outputStream,
            Dictionary<string, byte[]> files, string? password = null)
    {
        this.CreateArchive(this.ResolveFormat(formatName), outputStream, files, password);
    }

    /// <summary>Create an archive from files on disk by format name (e.g., "7z", "Zip", "Tar").</summary>
    public void CreateArchive(string formatName, Stream outputStream,
            Dictionary<string, string> files, string? password = null)
    {
        this.CreateArchive(this.ResolveFormat(formatName), outputStream, files, password);
    }

    private Guid ResolveFormat(string formatName)
    {
        var format = this.Formats.FirstOrDefault(f =>
            f.Name.Equals(formatName, StringComparison.OrdinalIgnoreCase));
        if (format == null)
        {
            throw new ArgumentException($"Unknown archive format: '{formatName}'.", nameof(formatName));
        }
        return format.ClassId;
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
        if (!this.disposed)
        {
            // Don't call NativeLibrary.Free — COM objects may still reference
            // the DLL's vtables. The OS unloads it at process exit.
            this.disposed = true;
        }
    }
}

/// <summary>Wrapper around IInArchive with the ComWrappers instance for CCW creation.</summary>
public sealed class ArchiveHandle : IDisposable
{
    public IInArchive Archive { get; }
    public StrategyBasedComWrappers ComWrappers { get; }

    // prevent GC of managed COM wrappers while native code holds references
    private InStreamWrapper? streamWrapper;
    private ArchiveOpenCallback? openCallback;
    private string? password;
    private IReadOnlyList<ArchiveEntry>? entries;

    internal ArchiveHandle(IInArchive archive, StrategyBasedComWrappers comWrappers)
    {
        this.Archive = archive;
        this.ComWrappers = comWrappers;
    }

    /// <summary>Open an archive from the given stream.</summary>
    public void Open(Stream stream, string? password = null, ulong maxCheckStartPosition = 1 << 23)
    {
        this.password = password;
        this.streamWrapper = new InStreamWrapper(stream);
        this.openCallback = new ArchiveOpenCallback(password);

        nint streamCcw = this.ComWrappers.GetOrCreateComInterfaceForObject(this.streamWrapper, CreateComInterfaceFlags.None);
        Guid iidInStream = Iid.IInStream;
        Marshal.QueryInterface(streamCcw, ref iidInStream, out nint streamPtr);

        nint callbackCcw = this.ComWrappers.GetOrCreateComInterfaceForObject(this.openCallback, CreateComInterfaceFlags.None);
        Guid iidCallback = Iid.IArchiveOpenCallback;
        Marshal.QueryInterface(callbackCcw, ref iidCallback, out nint callbackPtr);

        int hr;
        unsafe
        {
            ulong scanSize = maxCheckStartPosition;
            hr = this.Archive.Open(streamPtr, (nint)(&scanSize), callbackPtr);
        }

        // Release our QI references
        if (streamPtr != nint.Zero) { Marshal.Release(streamPtr); }
        if (callbackPtr != nint.Zero) { Marshal.Release(callbackPtr); }
        Marshal.Release(streamCcw);
        Marshal.Release(callbackCcw);

        Marshal.ThrowExceptionForHR(hr);
    }

    /// <summary>
    /// All entries in the archive with their metadata.
    /// Populated lazily on first access after Open().
    /// </summary>
    public IReadOnlyList<ArchiveEntry> Entries
    {
        get
        {
            if (this.entries == null)
            {
                this.entries = this.LoadEntries();
            }
            return this.entries;
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            this.Archive.Close();
            this.streamWrapper = null;
            this.openCallback = null;
        }
    }
    private bool disposed;

    /// <summary>Extract all files to memory. Returns dict of path → byte[].</summary>
    public Dictionary<string, byte[]> ExtractAll()
    {
        var callback = new ExtractCallback(this.Archive, this.ComWrappers, this.password);
        this.CallExtract(null, 0, callback);

        if (callback.Failures.Count > 0)
        {
            throw new ArchiveExtractionException(callback.Failures);
        }

        // Map index→bytes to path→bytes
        this.Archive.GetNumberOfItems(out uint count);
        var result = new Dictionary<string, byte[]>();
        for (uint i = 0; i < count; i++)
        {
            if (!callback.ExtractedData.TryGetValue(i, out var data))
            {
                continue;
            }
            PropVariant pv = default;
            this.Archive.GetProperty(i, PropId.kpidPath, ref pv);
            string path = pv.GetBstr() ?? i.ToString();
            NativeMethods.PropVariantClear(ref pv);
            result[path] = data;
        }
        return result;
    }

    /// <summary>Extract specific items by index. Returns dict of index → byte[].</summary>
    public Dictionary<uint, byte[]> Extract(uint[] indices)
    {
        var callback = new ExtractCallback(this.Archive, this.ComWrappers, this.password);
        this.CallExtract(indices, 0, callback);

        if (callback.Failures.Count > 0)
        {
            throw new ArchiveExtractionException(callback.Failures);
        }

        return callback.ExtractedData;
    }

    /// <summary>Test archive integrity (extract in verify-only mode).</summary>
    public void Test()
    {
        var callback = new ExtractCallback(this.Archive, this.ComWrappers, this.password);
        // numItems = 0xFFFFFFFF means "all", testMode = 1
        this.CallExtract(null, 1, callback);

        if (callback.Failures.Count > 0)
        {
            throw new ArchiveExtractionException(callback.Failures);
        }
    }

    /// <summary>Extract all files to the specified directory. Creates subdirectories as needed.</summary>
    public void ExtractTo(string directory)
    {
        Directory.CreateDirectory(directory);

        var callback = new DirectoryExtractCallback(
                this.Archive, this.ComWrappers, directory, this.password);

        var cw = this.ComWrappers;
        nint callbackCcw = cw.GetOrCreateComInterfaceForObject(callback, CreateComInterfaceFlags.None);
        Guid iidExtractCb = Iid.IArchiveExtractCallback;
        Marshal.QueryInterface(callbackCcw, ref iidExtractCb, out nint callbackPtr);

        int hr = this.Archive.Extract(nint.Zero, 0xFFFFFFFF, 0, callbackPtr);

        if (callbackPtr != nint.Zero) { Marshal.Release(callbackPtr); }
        Marshal.Release(callbackCcw);
        GC.KeepAlive(callback);
        Marshal.ThrowExceptionForHR(hr);

        if (callback.Failures.Count > 0)
        {
            throw new ArchiveExtractionException(callback.Failures);
        }
    }

    private void CallExtract(uint[]? indices, int testMode, ExtractCallback callback)
    {
        var cw = this.ComWrappers;
        nint callbackCcw = cw.GetOrCreateComInterfaceForObject(callback, CreateComInterfaceFlags.None);
        Guid iidExtractCb = Iid.IArchiveExtractCallback;
        Marshal.QueryInterface(callbackCcw, ref iidExtractCb, out nint callbackPtr);

        int hr;
        if (indices == null)
        {
            // Extract all: pass NULL indices and numItems = 0xFFFFFFFF
            hr = this.Archive.Extract(nint.Zero, 0xFFFFFFFF, testMode, callbackPtr);
        }
        else
        {
            // Sort a copy — 7-Zip requires indices in ascending order.
            // In solid archives, unsorted indices force the decoder to restart
            // decompression from the beginning, causing severe performance
            // degradation or incorrect results.
            var sorted = (uint[])indices.Clone();
            Array.Sort(sorted);

            unsafe
            {
                fixed (uint* pIndices = sorted)
                {
                    hr = this.Archive.Extract((nint)pIndices, (uint)sorted.Length, testMode, callbackPtr);
                }
            }
        }

        if (callbackPtr != nint.Zero) { Marshal.Release(callbackPtr); }
        Marshal.Release(callbackCcw);
        GC.KeepAlive(callback);
        Marshal.ThrowExceptionForHR(hr);
    }

    private List<ArchiveEntry> LoadEntries()
    {
        this.Archive.GetNumberOfItems(out uint count);
        var result = new List<ArchiveEntry>((int)count);

        for (uint i = 0; i < count; i++)
        {
            result.Add(this.LoadEntry(i));
        }

        return result;
    }

    private ArchiveEntry LoadEntry(uint index)
    {
        return new ArchiveEntry
        {
            Index = index,
            Path = this.GetStringProperty(index, PropId.kpidPath) ?? index.ToString(),
            IsDirectory = this.GetBoolProperty(index, PropId.kpidIsDir),
            Size = this.GetUInt64Property(index, PropId.kpidSize),
            PackedSize = this.GetUInt64Property(index, PropId.kpidPackSize),
            CreatedTime = this.GetFileTimeProperty(index, PropId.kpidCTime),
            ModifiedTime = this.GetFileTimeProperty(index, PropId.kpidMTime),
            AccessedTime = this.GetFileTimeProperty(index, PropId.kpidATime),
            IsEncrypted = this.GetBoolProperty(index, PropId.kpidEncrypted),
            Crc = this.GetNullableUInt32Property(index, PropId.kpidCRC),
            Method = this.GetStringProperty(index, PropId.kpidMethod),
        };
    }

    private string? GetStringProperty(uint index, uint propId)
    {
        PropVariant pv = default;
        this.Archive.GetProperty(index, propId, ref pv);
        string? value = pv.GetBstr();
        NativeMethods.PropVariantClear(ref pv);
        return value;
    }

    private bool GetBoolProperty(uint index, uint propId)
    {
        PropVariant pv = default;
        this.Archive.GetProperty(index, propId, ref pv);
        return pv.GetBool();
    }

    private ulong GetUInt64Property(uint index, uint propId)
    {
        PropVariant pv = default;
        this.Archive.GetProperty(index, propId, ref pv);
        return pv.GetUInt64();
    }

    private DateTime? GetFileTimeProperty(uint index, uint propId)
    {
        PropVariant pv = default;
        this.Archive.GetProperty(index, propId, ref pv);
        return pv.GetFileTime();
    }

    private uint? GetNullableUInt32Property(uint index, uint propId)
    {
        PropVariant pv = default;
        this.Archive.GetProperty(index, propId, ref pv);
        if (pv.VarType == PropVariant.VT_UI4)
        {
            return pv.UIntValue;
        }
        return null;
    }
}
