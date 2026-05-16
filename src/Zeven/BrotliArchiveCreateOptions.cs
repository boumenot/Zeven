namespace Zeven;

/// <summary>Options for standalone .br (Brotli) archive creation.</summary>
public class BrotliArchiveCreateOptions : IArchiveCreateOptions
{
    /// <summary>Compression level 0-11.</summary>
    public int? Level { get; init; }

    public IEnumerable<(string Name, object Value)> GetProperties()
    {
        if (this.Level.HasValue) { yield return (ArchivePropName.Level, (uint)this.Level.Value); }
    }
}
