namespace Zeven.Core.Interop;

/// <summary>
/// Well-known 7-Zip codec IDs used with <see cref="ZevenLibrary.FindCodecIndex"/>.
/// These are the internal method IDs from 7-Zip, not GUIDs.
/// </summary>
public static class CodecId
{
    // ── Upstream 7-Zip codecs ──
    public const ulong Copy      = 0x00;
    public const ulong Delta     = 0x03;
    public const ulong Lzma2     = 0x21;
    public const ulong Lzma      = 0x030101;
    public const ulong Ppmd      = 0x030401;
    public const ulong Deflate   = 0x040108;
    public const ulong Deflate64 = 0x040109;
    public const ulong BZip2     = 0x040202;
    public const ulong Aes256    = 0x06F10701;

    // ── 7-Zip-zstd additional codecs ──
    public const ulong Zstd      = 0x04F71101;
    public const ulong Brotli    = 0x04F71102;
    public const ulong Lz4       = 0x04F71104;
    public const ulong Lz5       = 0x04F71105;
    public const ulong Lizard    = 0x04F71106;
}

/// <summary>
/// 7-Zip COM interface IIDs and format CLSIDs.
/// All GUIDs follow the pattern {23170F69-40C1-278A-XXXX-XXXXXXXXXXXX}.
/// </summary>
public static class Iid
{
    // Stream interfaces (group 03)
    public static readonly Guid ISequentialInStream  = new("23170F69-40C1-278A-0000-000300010000");
    public static readonly Guid ISequentialOutStream = new("23170F69-40C1-278A-0000-000300020000");
    public static readonly Guid IInStream            = new("23170F69-40C1-278A-0000-000300030000");
    public static readonly Guid IOutStream           = new("23170F69-40C1-278A-0000-000300040000");

    // Archive interfaces (group 06)
    public static readonly Guid IArchiveOpenCallback    = new("23170F69-40C1-278A-0000-000600100000");
    public static readonly Guid IArchiveExtractCallback = new("23170F69-40C1-278A-0000-000600200000");
    public static readonly Guid IInArchive              = new("23170F69-40C1-278A-0000-000600600000");
    public static readonly Guid IArchiveUpdateCallback  = new("23170F69-40C1-278A-0000-000600800000");
    public static readonly Guid IOutArchive             = new("23170F69-40C1-278A-0000-000600A00000");

    // Property-setting interface (group 06)
    public static readonly Guid ISetProperties = new("23170F69-40C1-278A-0000-000600030000");

    // Codec interfaces (group 04)
    public static readonly Guid ICompressCoder               = new("23170F69-40C1-278A-0000-000400050000");
    public static readonly Guid ICompressSetCoderProperties   = new("23170F69-40C1-278A-0000-000400200000");
    public static readonly Guid ICompressSetDecoderProperties2 = new("23170F69-40C1-278A-0000-000400220000");
    public static readonly Guid ICompressWriteCoderProperties = new("23170F69-40C1-278A-0000-000400230000");
    public static readonly Guid ICompressSetInStream          = new("23170F69-40C1-278A-0000-000400310000");
    public static readonly Guid ICompressSetOutStreamSize     = new("23170F69-40C1-278A-0000-000400340000");
}

/// <summary>
/// Well-known 7-Zip archive format CLSIDs.
/// Pattern: {23170F69-40C1-278A-1000-000110XX0000} where XX is the format ID.
/// </summary>
public static class FormatClsid
{
    public static readonly Guid Zip   = new("23170F69-40C1-278A-1000-000110010000");
    public static readonly Guid BZip2 = new("23170F69-40C1-278A-1000-000110020000");
    public static readonly Guid Rar   = new("23170F69-40C1-278A-1000-000110030000");
    public static readonly Guid SevenZip = new("23170F69-40C1-278A-1000-000110070000");
    public static readonly Guid Cab   = new("23170F69-40C1-278A-1000-000110080000");
    public static readonly Guid Lzma  = new("23170F69-40C1-278A-1000-0001100A0000");
    public static readonly Guid Xz    = new("23170F69-40C1-278A-1000-0001100C0000");
    public static readonly Guid Tar   = new("23170F69-40C1-278A-1000-000110EE0000");
    public static readonly Guid GZip  = new("23170F69-40C1-278A-1000-000110EF0000");
}
