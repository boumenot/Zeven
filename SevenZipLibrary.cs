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
            NativeLibrary.Free(_lib);
            _disposed = true;
        }
    }
}

/// <summary>Wrapper around IInArchive with the ComWrappers instance for CCW creation.</summary>
public sealed class ArchiveHandle : IDisposable
{
    public IInArchive Archive { get; }
    public StrategyBasedComWrappers ComWrappers { get; }

    internal ArchiveHandle(IInArchive archive, StrategyBasedComWrappers comWrappers)
    {
        Archive = archive;
        ComWrappers = comWrappers;
    }

    public void Dispose()
    {
        Archive.Close();
    }
}
