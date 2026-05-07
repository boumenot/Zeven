using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>Options for standalone .br (Brotli) archive creation.</summary>
public class BrotliArchiveCreateOptions : IArchiveCreateOptions
{
    /// <summary>Compression level 0-11.</summary>
    public int? Level { get; init; }

    public void Apply(nint archivePtr, StrategyBasedComWrappers cw)
    {
        var props = new List<(string Name, object Value)>();
        if (this.Level.HasValue) { props.Add(("x", (uint)this.Level.Value)); }
        ArchiveOptions.ApplyProperties(archivePtr, cw, props);
    }
}
