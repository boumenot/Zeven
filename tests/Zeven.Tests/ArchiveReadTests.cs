using System.Diagnostics;
using Zeven;

namespace Zeven.Tests;

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
            var psi = new ProcessStartInfo(TestPaths.ExePath)
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
    static string DllPath => TestPaths.DllPath;
    private readonly ArchiveFixture _fixture;

    public ArchiveReadTests(ArchiveFixture fixture) => _fixture = fixture;

    [Fact]
    public void Open_FromMemoryStream_Succeeds()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        Assert.Equal(_fixture.OriginalFiles.Count, handle.Entries.Count);
    }

    [Fact]
    public void GetProperty_ReturnsCorrectPaths()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        var paths = handle.Entries.Select(e => e.Path).ToList();

        foreach (var name in _fixture.OriginalFiles.Keys)
            Assert.Contains(name, paths);
    }

    [Fact]
    public void GetProperty_ReturnsCorrectSizes()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        foreach (var entry in handle.Entries)
        {
            Assert.Equal((ulong)_fixture.OriginalFiles[entry.Path].Length, entry.Size);
        }
    }

    [Fact]
    public void Entries_ReturnsCorrectCount()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        Assert.Equal(_fixture.OriginalFiles.Count, handle.Entries.Count);
    }

    [Fact]
    public void Entries_ContainCorrectPaths()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        var paths = handle.Entries.Select(e => e.Path).OrderBy(p => p).ToList();
        Assert.Contains("hello.txt", paths);
    }

    [Fact]
    public void Entries_HaveSize()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        var entry = handle.Entries.First(e => e.Path == "hello.txt");
        Assert.True(entry.Size > 0);
        Assert.False(entry.IsDirectory);
    }

    [Fact]
    public void Entries_HaveModifiedTime()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        var entry = handle.Entries.First(e => e.Path == "hello.txt");
        Assert.NotNull(entry.ModifiedTime);
    }

    [Fact]
    public void Entries_IndexMatchesPosition()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, stream);

        for (int i = 0; i < handle.Entries.Count; i++)
        {
            Assert.Equal((uint)i, handle.Entries[i].Index);
        }
    }
}
