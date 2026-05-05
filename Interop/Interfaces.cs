using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipNet.Interop;

/// <summary>ISequentialInStream — provides sequential read access.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000300010000")]
internal partial interface ISequentialInStream
{
    [PreserveSig]
    int Read(nint data, uint size, out uint processedSize);
}

/// <summary>IInStream — adds seeking to ISequentialInStream.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000300030000")]
internal partial interface IInStream : ISequentialInStream
{
    // newPosition is nint (not out ulong) because 7-Zip's InStream_SeekSet passes NULL
    [PreserveSig]
    int Seek(long offset, uint seekOrigin, nint newPosition);
}

/// <summary>IArchiveOpenCallback — receives progress during archive open.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600100000")]
internal partial interface IArchiveOpenCallback
{
    [PreserveSig]
    int SetTotal(nint files, nint bytes);

    [PreserveSig]
    int SetCompleted(nint files, nint bytes);
}

/// <summary>
/// IInArchive — main interface for reading archives.
/// Methods must appear in exact vtable order matching the C++ definition.
/// </summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600600000")]
internal partial interface IInArchive
{
    [PreserveSig]
    int Open(nint stream, nint maxCheckStartPosition, nint openCallback);

    [PreserveSig]
    int Close();

    [PreserveSig]
    int GetNumberOfItems(out uint numItems);

    [PreserveSig]
    int GetProperty(uint index, uint propID, ref PropVariant value);

    [PreserveSig]
    int Extract(nint indices, uint numItems, int testMode, nint extractCallback);

    [PreserveSig]
    int GetArchiveProperty(uint propID, ref PropVariant value);

    [PreserveSig]
    int GetNumberOfProperties(out uint numProps);

    [PreserveSig]
    int GetPropertyInfo(uint index, out nint name, out uint propID, out ushort varType);

    [PreserveSig]
    int GetNumberOfArchiveProperties(out uint numProps);

    [PreserveSig]
    int GetArchivePropertyInfo(uint index, out nint name, out uint propID, out ushort varType);
}
