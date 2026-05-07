using System.Runtime.InteropServices.Marshalling;

namespace Zeven.Core;

/// <summary>Options for xz archive creation.</summary>
public class XzCreateOptions : IArchiveCreateOptions
{
        /// <summary>Compression level 0-9.</summary>
        public int? Level { get; init; }

        /// <summary>Number of CPU threads.</summary>
        public int? NumThreads { get; init; }

        public void Apply(nint archivePtr, StrategyBasedComWrappers cw)
        {
                var props = new List<(string Name, object Value)>();
                if (this.Level.HasValue) { props.Add(("x", (uint)this.Level.Value)); }
                if (this.NumThreads.HasValue) { props.Add(("mt", (uint)this.NumThreads.Value)); }
                ArchiveOptions.ApplyProperties(archivePtr, cw, props);
        }
}
