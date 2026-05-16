namespace Zeven;

/// <summary>Options for gzip archive creation.</summary>
public class GZipCreateOptions : IArchiveCreateOptions
{
        /// <summary>Compression level 0-9.</summary>
        public int? Level { get; init; }

        public IEnumerable<(string Name, object Value)> GetProperties()
        {
                if (this.Level.HasValue) { yield return (ArchivePropName.Level, (uint)this.Level.Value); }
        }
}
