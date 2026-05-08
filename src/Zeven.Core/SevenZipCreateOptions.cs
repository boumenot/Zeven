namespace Zeven.Core;

/// <summary>Options for .7z archive creation.</summary>
public class SevenZipCreateOptions : IArchiveCreateOptions
{
    /// <summary>Compression level 0-9.</summary>
    public int? Level { get; init; }

    /// <summary>Compression method: "LZMA2", "PPMd", "Zstd", "BZip2", "Deflate", "Copy".</summary>
    public string? Method { get; init; }

    /// <summary>Enable solid archive mode.</summary>
    public bool? Solid { get; init; }

    /// <summary>Number of CPU threads.</summary>
    public int? NumThreads { get; init; }

    /// <summary>Encrypt file names in addition to content.</summary>
    public bool? EncryptHeaders { get; init; }

    public IEnumerable<(string Name, object Value)> GetProperties()
    {
        if (this.Level.HasValue) { yield return ("x", (uint)this.Level.Value); }
        if (this.Method != null) { yield return ("0", this.Method); }
        if (this.Solid.HasValue) { yield return ("s", this.Solid.Value); }
        if (this.NumThreads.HasValue) { yield return ("mt", (uint)this.NumThreads.Value); }
        if (this.EncryptHeaders.HasValue) { yield return ("he", this.EncryptHeaders.Value); }
    }
}
