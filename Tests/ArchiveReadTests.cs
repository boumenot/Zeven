using System.Diagnostics;
using System.Runtime.InteropServices;
using SevenZipNet;
using SevenZipNet.Interop;

namespace SevenZipNet.Tests;

/// <summary>
/// Creates a .7z archive in memory (via 7za.exe + temp file) for use as test fixture.
/// All other tests operate on in-memory streams only.
/// </summary>
public class ArchiveFixture : IDisposable
{
    public byte[] ArchiveBytes { get; }
    public Dictionary<string, byte[]> OriginalFiles { get; } = new();

    public ArchiveFixture()
    {
        // Build known file contents
        OriginalFiles["hello.txt"] = "Hello, World!"u8.ToArray();
        OriginalFiles["data.csv"] = "Name,Value\nAlpha,1\nBeta,2\n"u8.ToArray();

        // Create archive via 7za.exe (one-time setup)
        var tempDir = Path.Combine(Path.GetTempPath(), $"7ztest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            foreach (var (name, content) in OriginalFiles)
                File.WriteAllBytes(Path.Combine(tempDir, name), content);

            var archivePath = Path.Combine(tempDir, "test.7z");
            var psi = new ProcessStartInfo(@"q:\7z2601-bin\x64\7za.exe")
            {
                Arguments = $"a -t7z \"{archivePath}\" \"{tempDir}\\*\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            Process.Start(psi)!.WaitForExit();
            ArchiveBytes = File.ReadAllBytes(archivePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    public void Dispose() { }
}

public class ArchiveReadTests : IClassFixture<ArchiveFixture>
{
    const string DllPath = @"q:\7z2601-bin\x64\7za.dll";
    private readonly ArchiveFixture _fixture;

    public ArchiveReadTests(ArchiveFixture fixture) => _fixture = fixture;

    [Fact]
    public void Open_FromMemoryStream_Succeeds()
    {
        using var lib = new SevenZipLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var handle = lib.CreateInArchive(format.ClassId);
        using var stream = new MemoryStream(_fixture.ArchiveBytes);

        handle.Open(stream);

        handle.Archive.GetNumberOfItems(out uint count);
        Assert.Equal((uint)_fixture.OriginalFiles.Count, count);
    }

    [Fact]
    public void GetProperty_ReturnsCorrectPaths()
    {
        using var lib = new SevenZipLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var handle = lib.CreateInArchive(format.ClassId);
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        handle.Open(stream);

        handle.Archive.GetNumberOfItems(out uint count);
        var paths = new List<string>();
        for (uint i = 0; i < count; i++)
        {
            PropVariant pv = default;
            handle.Archive.GetProperty(i, PropId.kpidPath, ref pv);
            paths.Add(pv.GetBstr()!);
            NativeMethods.PropVariantClear(ref pv);
        }

        foreach (var name in _fixture.OriginalFiles.Keys)
            Assert.Contains(name, paths);
    }

    [Fact]
    public void GetProperty_ReturnsCorrectSizes()
    {
        using var lib = new SevenZipLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var handle = lib.CreateInArchive(format.ClassId);
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        handle.Open(stream);

        handle.Archive.GetNumberOfItems(out uint count);
        for (uint i = 0; i < count; i++)
        {
            PropVariant pvPath = default;
            handle.Archive.GetProperty(i, PropId.kpidPath, ref pvPath);
            string path = pvPath.GetBstr()!;
            NativeMethods.PropVariantClear(ref pvPath);

            PropVariant pvSize = default;
            handle.Archive.GetProperty(i, PropId.kpidSize, ref pvSize);
            ulong size = pvSize.GetUInt64();

            Assert.Equal((ulong)_fixture.OriginalFiles[path].Length, size);
        }
    }
}
