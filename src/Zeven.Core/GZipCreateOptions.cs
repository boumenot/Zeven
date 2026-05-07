using System.Runtime.InteropServices.Marshalling;

namespace Zeven.Core;

/// <summary>Options for gzip archive creation.</summary>
public class GZipCreateOptions : IArchiveCreateOptions
{
        /// <summary>Compression level 0-9.</summary>
        public int? Level { get; init; }

        public void Apply(nint archivePtr, StrategyBasedComWrappers cw)
        {
                var props = new List<(string Name, object Value)>();
                if (this.Level.HasValue) { props.Add(("x", (uint)this.Level.Value)); }
                ArchiveOptions.ApplyProperties(archivePtr, cw, props);
        }
}
