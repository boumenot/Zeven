using System.Runtime.InteropServices.Marshalling;

namespace Zeven.Core;

/// <summary>Options for bzip2 archive creation.</summary>
public class BZip2CreateOptions : IArchiveCreateOptions
{
        /// <summary>Compression level 0-9.</summary>
        public int? Level { get; init; }

        /// <summary>Number of passes for compression.</summary>
        public int? NumPasses { get; init; }

        public void Apply(nint archivePtr, StrategyBasedComWrappers cw)
        {
                var props = new List<(string Name, object Value)>();
                if (this.Level.HasValue) { props.Add(("x", (uint)this.Level.Value)); }
                if (this.NumPasses.HasValue) { props.Add(("pass", (uint)this.NumPasses.Value)); }
                ArchiveOptionsHelper.ApplyProperties(archivePtr, cw, props);
        }
}
