using System.Runtime.InteropServices.Marshalling;

namespace Zeven.Core;

/// <summary>Options for .7z archive creation.</summary>
public class SevenZipCreateOptions : IArchiveCreateOptions
{
    /// <summary>Compression level 0-9.</summary>
    public int? Level { get; init; }

    /// <summary>Compression method: "LZMA2", "PPMd", "Zstd", "BZip2", "Deflate", "Copy".</summary>
    public string? Method { get; init; }

    /// <summary>Enable solid archive mode.</summary>
    public bool? Solid { get; init; }

    /// <summary>Number of CPU threads.</summary>
    public int? NumThreads { get; init; }

    /// <summary>Encrypt file names in addition to content.</summary>
    public bool? EncryptHeaders { get; init; }

    public void Apply(nint archivePtr, StrategyBasedComWrappers cw)
    {
        var props = new List<(string Name, object Value)>();
        if (this.Level.HasValue) { props.Add(("x", (uint)this.Level.Value)); }
        if (this.Method != null) { props.Add(("0", this.Method)); }
        if (this.Solid.HasValue) { props.Add(("s", this.Solid.Value)); }
        if (this.NumThreads.HasValue) { props.Add(("mt", (uint)this.NumThreads.Value)); }
        if (this.EncryptHeaders.HasValue) { props.Add(("he", this.EncryptHeaders.Value)); }
        ArchiveOptions.ApplyProperties(archivePtr, cw, props);
    }
}
