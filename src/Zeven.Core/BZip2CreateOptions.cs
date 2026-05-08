namespace Zeven.Core;

/// <summary>Options for bzip2 archive creation.</summary>
public class BZip2CreateOptions : IArchiveCreateOptions
{
        /// <summary>Compression level 0-9.</summary>
        public int? Level { get; init; }

        /// <summary>Number of passes for compression.</summary>
        public int? NumPasses { get; init; }

        public IEnumerable<(string Name, object Value)> GetProperties()
        {
                if (this.Level.HasValue) { yield return (ArchivePropName.Level, (uint)this.Level.Value); }
                if (this.NumPasses.HasValue) { yield return (ArchivePropName.NumPasses, (uint)this.NumPasses.Value); }
        }
}
