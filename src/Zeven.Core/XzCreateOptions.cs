namespace Zeven.Core;

/// <summary>Options for xz archive creation.</summary>
public class XzCreateOptions : IArchiveCreateOptions
{
        /// <summary>Compression level 0-9.</summary>
        public int? Level { get; init; }

        /// <summary>Number of CPU threads.</summary>
        public int? NumThreads { get; init; }

        public IEnumerable<(string Name, object Value)> GetProperties()
        {
                if (this.Level.HasValue) { yield return (ArchivePropName.Level, (uint)this.Level.Value); }
                if (this.NumThreads.HasValue) { yield return (ArchivePropName.NumThreads, (uint)this.NumThreads.Value); }
        }
}
