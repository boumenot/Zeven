using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>Options for standalone .br (Brotli) archive creation.</summary>
public class BrotliArchiveCreateOptions : IArchiveCreateOptions
{
    /// <summary>Compression level 0-11.</summary>
    public int? Level { get; init; }

    /// <summary>Number of CPU threads.</summary>
    public int? NumThreads { get; init; }

    public void Apply(nint archivePtr, StrategyBasedComWrappers cw)
    {
        var props = new List<(string Name, object Value)>();
        if (this.Level.HasValue) { props.Add(("x", (uint)this.Level.Value)); }
        if (this.NumThreads.HasValue) { props.Add(("mt", (uint)this.NumThreads.Value)); }
        ArchiveOptionsHelper.ApplyProperties(archivePtr, cw, props);
    }
}
