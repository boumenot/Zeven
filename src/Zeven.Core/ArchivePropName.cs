namespace Zeven.Core;

/// <summary>
/// 7-Zip archive property names passed to ISetProperties.
/// These match the command-line switch names used by 7z.exe.
/// </summary>
internal static class ArchivePropName
{
    /// <summary>Compression level (0-9). CLI: -mx=N</summary>
    public const string Level = "x";

    /// <summary>Compression method for the first coder. CLI: -m0=METHOD</summary>
    public const string Method = "0";

    /// <summary>Solid archive mode. CLI: -ms=on/off</summary>
    public const string Solid = "s";

    /// <summary>Number of CPU threads. CLI: -mmt=N</summary>
    public const string NumThreads = "mt";

    /// <summary>Encrypt archive headers (file names). CLI: -mhe=on</summary>
    public const string EncryptHeaders = "he";

    /// <summary>Number of encoding passes (BZip2). CLI: -mpass=N</summary>
    public const string NumPasses = "pass";
}
