using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Zeven.Core.Interop;

// ── Stream interfaces ───────────────────────────────────────────────────────

/// <summary>ISequentialInStream — provides sequential read access.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000300010000")]
public partial interface ISequentialInStream
{
    [PreserveSig]
    int Read(nint data, uint size, out uint processedSize);
}

/// <summary>IInStream — adds seeking to ISequentialInStream.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000300030000")]
public partial interface IInStream : ISequentialInStream
{
    // newPosition is nint (not out ulong) because 7-Zip's InStream_SeekSet passes NULL
    [PreserveSig]
    int Seek(long offset, uint seekOrigin, nint newPosition);
}

/// <summary>ISequentialOutStream — provides sequential write access.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000300020000")]
public partial interface ISequentialOutStream
{
    [PreserveSig]
    int Write(nint data, uint size, out uint processedSize);
}

/// <summary>IOutStream — adds seeking and resizing to ISequentialOutStream.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000300040000")]
public partial interface IOutStream : ISequentialOutStream
{
    [PreserveSig]
    int Seek(long offset, uint seekOrigin, nint newPosition);

    [PreserveSig]
    int SetSize(ulong newSize);
}

// ── Progress ────────────────────────────────────────────────────────────────

/// <summary>IProgress — base progress interface (group=0, sub=5).</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000000050000")]
public partial interface IProgress
{
    [PreserveSig]
    int SetTotal(ulong total);

    [PreserveSig]
    int SetCompleted(nint completeValue); // const UInt64* — nullable
}

// ── Archive open ────────────────────────────────────────────────────────────

/// <summary>IArchiveOpenCallback — receives progress during archive open.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600100000")]
public partial interface IArchiveOpenCallback
{
    [PreserveSig]
    int SetTotal(nint files, nint bytes);

    [PreserveSig]
    int SetCompleted(nint files, nint bytes);
}

// ── Archive extract ─────────────────────────────────────────────────────────

/// <summary>IArchiveExtractCallback — provides output streams during extraction.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600200000")]
public partial interface IArchiveExtractCallback : IProgress
{
    [PreserveSig]
    int GetStream(uint index, out nint outStream, int askExtractMode);

    [PreserveSig]
    int PrepareOperation(int askExtractMode);

    [PreserveSig]
    int SetOperationResult(int opRes);
}

// ── IInArchive ──────────────────────────────────────────────────────────────

/// <summary>
/// IInArchive — main interface for reading archives.
/// Methods must appear in exact vtable order matching the C++ definition.
/// </summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600600000")]
public partial interface IInArchive
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

// ── IOutArchive ─────────────────────────────────────────────────────────────

/// <summary>IOutArchive — creates/updates archives.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600A00000")]
public partial interface IOutArchive
{
    [PreserveSig]
    int UpdateItems(nint outStream, uint numItems, nint updateCallback);

    [PreserveSig]
    int GetFileTimeType(out uint type);
}

// ── ISetProperties ──────────────────────────────────────────────────────────

/// <summary>ISetProperties — configures compression options.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600030000")]
public partial interface ISetProperties
{
    [PreserveSig]
    int SetProperties(nint names, nint values, uint numProps);
}

// ── Archive update callbacks ────────────────────────────────────────────────

/// <summary>IArchiveUpdateCallback — provides file data during archive creation.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600800000")]
public partial interface IArchiveUpdateCallback : IProgress
{
    [PreserveSig]
    int GetUpdateItemInfo(uint index, out int newData, out int newProps, out uint indexInArchive);

    [PreserveSig]
    int GetProperty(uint index, uint propID, ref PropVariant value);

    [PreserveSig]
    int GetStream(uint index, out nint inStream);

    [PreserveSig]
    int SetOperationResult(int operationResult);
}

// ── Password interfaces ─────────────────────────────────────────────────────

/// <summary>ICryptoGetTextPassword — provides password for reading encrypted archives.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000500100000")]
public partial interface ICryptoGetTextPassword
{
    [PreserveSig]
    int CryptoGetTextPassword(out nint password); // BSTR*
}

/// <summary>ICryptoGetTextPassword2 — provides password for creating encrypted archives.</summary>
[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000500110000")]
public partial interface ICryptoGetTextPassword2
{
    [PreserveSig]
    int CryptoGetTextPassword2(out int passwordIsDefined, out nint password); // BSTR*
}
