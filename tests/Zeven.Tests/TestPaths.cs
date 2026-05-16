using System;

namespace Zeven.Tests;

/// <summary>
/// Resolves native DLL paths from the ZEVEN_7Z_DLL_PATH environment variable,
/// falling back to the default bin directory.
/// </summary>
internal static class TestPaths
{
    internal static readonly string DllPath =
        Environment.GetEnvironmentVariable("ZEVEN_7Z_DLL_PATH")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "bin", "7z.dll"));

    internal static readonly string ExePath =
        Path.Combine(Path.GetDirectoryName(DllPath)!, "7za.exe");
}
