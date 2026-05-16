using Zeven;

namespace Zeven.Tests;

public class ArchiveUpdateTests
{
    static string DllPath => TestPaths.DllPath;

    [Fact]
    public void UpdateArchive_AddFile_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        // Create initial archive
        var files = new Dictionary<string, byte[]>
        {
            ["original.txt"] = "original"u8.ToArray(),
        };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        // Update: add a new file
        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, u => u
            .Add("new.txt", "new content"u8.ToArray()));

        // Verify
        updated.Position = 0;
        using var verify = lib.OpenArchive("7z", updated);
        var extracted = verify.ExtractAll();
        Assert.Equal(2, extracted.Count);
        Assert.Equal("original", System.Text.Encoding.UTF8.GetString(extracted["original.txt"]));
        Assert.Equal("new content", System.Text.Encoding.UTF8.GetString(extracted["new.txt"]));
    }

    [Fact]
    public void UpdateArchive_DeleteFile_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        var files = new Dictionary<string, byte[]>
        {
            ["keep.txt"] = "keep"u8.ToArray(),
            ["delete.txt"] = "delete me"u8.ToArray(),
        };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, u => u
            .Delete("delete.txt"));

        updated.Position = 0;
        using var verify = lib.CreateInArchive("7z");
        verify.Open(updated);
        var extracted = verify.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal("keep", System.Text.Encoding.UTF8.GetString(extracted["keep.txt"]));
    }

    [Fact]
    public void UpdateArchive_ReplaceFile_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        var files = new Dictionary<string, byte[]>
        {
            ["config.json"] = "{\"v\":1}"u8.ToArray(),
        };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, u => u
            .Replace("config.json", "{\"v\":2}"u8.ToArray()));

        updated.Position = 0;
        using var verify = lib.CreateInArchive("7z");
        verify.Open(updated);
        var extracted = verify.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal("{\"v\":2}", System.Text.Encoding.UTF8.GetString(extracted["config.json"]));
    }

    [Fact]
    public void UpdateArchive_FluentChain_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        var files = new Dictionary<string, byte[]>
        {
            ["a.txt"] = "AAA"u8.ToArray(),
            ["b.txt"] = "BBB"u8.ToArray(),
            ["c.txt"] = "CCC"u8.ToArray(),
        };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, u => u
            .Delete("b.txt")
            .Replace("c.txt", "CCC-updated"u8.ToArray())
            .Add("d.txt", "DDD"u8.ToArray()));

        updated.Position = 0;
        using var verify = lib.CreateInArchive("7z");
        verify.Open(updated);
        var extracted = verify.ExtractAll();
        Assert.Equal(3, extracted.Count);
        Assert.Equal("AAA", System.Text.Encoding.UTF8.GetString(extracted["a.txt"]));
        Assert.Equal("CCC-updated", System.Text.Encoding.UTF8.GetString(extracted["c.txt"]));
        Assert.Equal("DDD", System.Text.Encoding.UTF8.GetString(extracted["d.txt"]));
        Assert.False(extracted.ContainsKey("b.txt"));
    }

    [Fact]
    public void UpdateArchive_NonFluent_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        var files = new Dictionary<string, byte[]>
        {
            ["a.txt"] = "AAA"u8.ToArray(),
            ["b.txt"] = "BBB"u8.ToArray(),
        };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, update =>
        {
            update.Add("c.txt", "CCC"u8.ToArray());
            update.Delete("b.txt");
        });

        updated.Position = 0;
        using var verify = lib.OpenArchive("7z", updated);
        var extracted = verify.ExtractAll();
        Assert.Equal(2, extracted.Count);
        Assert.Equal("AAA", System.Text.Encoding.UTF8.GetString(extracted["a.txt"]));
        Assert.Equal("CCC", System.Text.Encoding.UTF8.GetString(extracted["c.txt"]));
    }

    [Fact]
    public void UpdateArchive_AddFromFilePath_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var files = new Dictionary<string, byte[]> { ["a.txt"] = "AAA"u8.ToArray() };
            using var initial = new MemoryStream();
            lib.CreateArchive("7z", initial, files);

            File.WriteAllText(Path.Combine(tempDir, "added.txt"), "from disk");

            initial.Position = 0;
            using var handle = lib.OpenArchive("7z", initial);

            using var updated = new MemoryStream();
            lib.UpdateArchive("7z", handle, updated, u => u
                .Add("added.txt", Path.Combine(tempDir, "added.txt")));

            updated.Position = 0;
            using var verify = lib.OpenArchive("7z", updated);
            var extracted = verify.ExtractAll();
            Assert.Equal(2, extracted.Count);
            Assert.Equal("from disk", System.Text.Encoding.UTF8.GetString(extracted["added.txt"]));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void UpdateArchive_AddFromStream_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        var files = new Dictionary<string, byte[]> { ["a.txt"] = "AAA"u8.ToArray() };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        var streamData = "from stream"u8.ToArray();
        using var dataStream = new MemoryStream(streamData);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, u => u
            .Add("streamed.txt", dataStream, streamData.Length));

        updated.Position = 0;
        using var verify = lib.OpenArchive("7z", updated);
        var extracted = verify.ExtractAll();
        Assert.Equal(2, extracted.Count);
        Assert.Equal("from stream", System.Text.Encoding.UTF8.GetString(extracted["streamed.txt"]));
    }

    [Fact]
    public void UpdateArchive_ReplaceFromFilePath_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var files = new Dictionary<string, byte[]> { ["config.txt"] = "v1"u8.ToArray() };
            using var initial = new MemoryStream();
            lib.CreateArchive("7z", initial, files);

            File.WriteAllText(Path.Combine(tempDir, "config.txt"), "v2 from disk");

            initial.Position = 0;
            using var handle = lib.OpenArchive("7z", initial);

            using var updated = new MemoryStream();
            lib.UpdateArchive("7z", handle, updated, u => u
                .Replace("config.txt", Path.Combine(tempDir, "config.txt")));

            updated.Position = 0;
            using var verify = lib.OpenArchive("7z", updated);
            var extracted = verify.ExtractAll();
            Assert.Single(extracted);
            Assert.Equal("v2 from disk", System.Text.Encoding.UTF8.GetString(extracted["config.txt"]));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void UpdateArchive_ReplaceFromStream_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        var files = new Dictionary<string, byte[]> { ["config.txt"] = "v1"u8.ToArray() };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        var streamData = "v2 from stream"u8.ToArray();
        using var dataStream = new MemoryStream(streamData);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, u => u
            .Replace("config.txt", dataStream, streamData.Length));

        updated.Position = 0;
        using var verify = lib.OpenArchive("7z", updated);
        var extracted = verify.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal("v2 from stream", System.Text.Encoding.UTF8.GetString(extracted["config.txt"]));
    }

    [Fact]
    public void UpdateArchive_Replace_UsesExactPathMatch()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["File.txt"] = "original"u8.ToArray(),
        };
        using var initial = new MemoryStream();
        lib.CreateArchive("7z", initial, files);

        initial.Position = 0;
        using var handle = lib.OpenArchive("7z", initial);

        using var updated = new MemoryStream();
        lib.UpdateArchive("7z", handle, updated, u => u
            .Replace("File.txt", "updated"u8.ToArray()));

        updated.Position = 0;
        using var verify = lib.OpenArchive("7z", updated);
        var extracted = verify.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal("updated", System.Text.Encoding.UTF8.GetString(extracted["File.txt"]));
    }
}
