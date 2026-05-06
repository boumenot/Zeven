using Zeven.Core;
using Zeven.Core.Interop;

namespace Zeven.Tests;

public class ArchiveCreateTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    [Fact]
    public void CreateArchive_RoundTrip_ProducesIdenticalContent()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["hello.txt"] = "Hello, World!"u8.ToArray(),
            ["sub/data.bin"] = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD },
        };

        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        // Create archive to MemoryStream
        byte[] archiveBytes;
        using (var outStream = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, outStream, files);
            archiveBytes = outStream.ToArray();
        }

        Assert.True(archiveBytes.Length > 0, "Archive should be non-empty");

        // Read it back and verify
        using var handle = lib.CreateInArchive(format.ClassId);
        using var inStream = new MemoryStream(archiveBytes);
        handle.Open(inStream);

        var extracted = handle.ExtractAll();

        Assert.Equal(files.Count, extracted.Count);
        foreach (var (name, expected) in files)
        {
            var key = extracted.Keys.First(k => k.Replace('\\', '/') == name.Replace('\\', '/'));
            Assert.Equal(expected, extracted[key]);
        }
    }

    [Fact]
    public void CreateArchive_EmptyFile_RoundTrips()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["empty.txt"] = Array.Empty<byte>(),
        };

        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        using var outStream = new MemoryStream();
        lib.CreateArchive(format.ClassId, outStream, files);
        byte[] archiveBytes = outStream.ToArray();

        using var handle = lib.CreateInArchive(format.ClassId);
        using var inStream = new MemoryStream(archiveBytes);
        handle.Open(inStream);

        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
        Assert.Empty(extracted["empty.txt"]);
    }

    [Fact]
    public void CreateArchive_LargeFile_RoundTrips()
    {
        // 1MB of pseudo-random data
        var rng = new Random(42);
        var largeData = new byte[1024 * 1024];
        rng.NextBytes(largeData);

        var files = new Dictionary<string, byte[]>
        {
            ["large.bin"] = largeData,
        };

        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        using var outStream = new MemoryStream();
        lib.CreateArchive(format.ClassId, outStream, files);
        byte[] archiveBytes = outStream.ToArray();

        using var handle = lib.CreateInArchive(format.ClassId);
        using var inStream = new MemoryStream(archiveBytes);
        handle.Open(inStream);

        var extracted = handle.ExtractAll();
        Assert.Equal(largeData, extracted["large.bin"]);
    }
}
