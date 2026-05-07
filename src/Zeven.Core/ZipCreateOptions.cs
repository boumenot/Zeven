using System.Runtime.InteropServices.Marshalling;

namespace Zeven.Core;

/// <summary>Options for .zip archive creation.</summary>
public class ZipCreateOptions : IArchiveCreateOptions
{
    /// <summary>Compression level 0-9.</summary>
    public int? Level { get; init; }

    /// <summary>Compression method: "Deflate", "Deflate64", "BZip2", "LZMA", "PPMd", "Copy".</summary>
    public string? Method { get; init; }

    /// <summary>Number of CPU threads.</summary>
    public int? NumThreads { get; init; }

    public void Apply(nint archivePtr, StrategyBasedComWrappers cw)
    {
        var props = new List<(string Name, object Value)>();
        if (this.Level.HasValue) { props.Add(("x", (uint)this.Level.Value)); }
        if (this.Method != null) { props.Add(("0", this.Method)); }
        if (this.NumThreads.HasValue) { props.Add(("mt", (uint)this.NumThreads.Value)); }
        ArchiveOptionsHelper.ApplyProperties(archivePtr, cw, props);
    }
}
