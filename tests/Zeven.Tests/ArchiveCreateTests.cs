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

    [Fact]
    public void CreateArchive_FromDiskFiles_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create source files
            File.WriteAllText(Path.Combine(tempDir, "hello.txt"), "Hello from disk!");
            File.WriteAllBytes(Path.Combine(tempDir, "data.bin"), new byte[1000]);

            var files = new Dictionary<string, string>
            {
                ["hello.txt"] = Path.Combine(tempDir, "hello.txt"),
                ["data.bin"] = Path.Combine(tempDir, "data.bin"),
            };

            // Create archive
            using var archiveStream = new MemoryStream();
            lib.CreateArchive(FormatClsid.SevenZip, archiveStream, files);

            // Verify by extracting with existing API
            archiveStream.Position = 0;
            using var handle = lib.CreateInArchive(FormatClsid.SevenZip);
            handle.Open(archiveStream);
            var extracted = handle.ExtractAll();

            Assert.Equal(2, extracted.Count);
            Assert.Equal("Hello from disk!", System.Text.Encoding.UTF8.GetString(extracted["hello.txt"]));
            Assert.Equal(1000, extracted["data.bin"].Length);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ExtractTo_WritesFilesToDisk()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            // Create an archive with known content
            var files = new Dictionary<string, byte[]>
            {
                ["doc.txt"] = System.Text.Encoding.UTF8.GetBytes("Test content"),
                ["binary.dat"] = new byte[500],
            };
            using var archiveStream = new MemoryStream();
            lib.CreateArchive(FormatClsid.SevenZip, archiveStream, files);

            // Extract to disk
            archiveStream.Position = 0;
            using var handle = lib.CreateInArchive(FormatClsid.SevenZip);
            handle.Open(archiveStream);

            var extractDir = Path.Combine(tempDir, "extracted");
            handle.ExtractTo(extractDir);

            Assert.True(File.Exists(Path.Combine(extractDir, "doc.txt")));
            Assert.Equal("Test content", File.ReadAllText(Path.Combine(extractDir, "doc.txt")));
            Assert.True(File.Exists(Path.Combine(extractDir, "binary.dat")));
            Assert.Equal(500, new FileInfo(Path.Combine(extractDir, "binary.dat")).Length);
        }
        finally
        {
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
        }
    }

    [Fact]
    public void ValidatePath_NormalPath_Succeeds()
    {
        string result = DirectoryExtractCallback.ValidatePathInternal(@"C:\output\", "docs/file.txt");
        Assert.StartsWith(@"C:\output\", result);
    }

    [Fact]
    public void ValidatePath_TraversalPath_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => DirectoryExtractCallback.ValidatePathInternal(@"C:\output\", @"..\..\evil.txt"));
    }

    [Fact]
    public void ValidatePath_AbsolutePath_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => DirectoryExtractCallback.ValidatePathInternal(@"C:\output\", @"C:\Windows\evil.exe"));
    }

    [Fact]
    public void CreateArchive_WithLevel_ProducesSmallerOutput()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = new byte[100_000];
        new Random(42).NextBytes(data);
        var files = new Dictionary<string, byte[]> { ["data.bin"] = data };

        using var fast = new MemoryStream();
        lib.CreateArchive("7z", fast, files, new SevenZipCreateOptions { Level = 1 });

        using var ultra = new MemoryStream();
        lib.CreateArchive("7z", ultra, files, new SevenZipCreateOptions { Level = 9 });

        Assert.True(ultra.Length <= fast.Length,
            $"Level 9 ({ultra.Length}) should be <= level 1 ({fast.Length})");
    }

    [Fact]
    public void CreateArchive_WithMethod_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["test.txt"] = System.Text.Encoding.UTF8.GetBytes("Hello PPMd"),
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, new SevenZipCreateOptions { Method = "PPMd" });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Equal("Hello PPMd", System.Text.Encoding.UTF8.GetString(extracted.Values.First()));
    }

    [Fact]
    public void CreateArchive_WithEncryptedHeaders()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = System.Text.Encoding.UTF8.GetBytes("classified"),
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, new SevenZipCreateOptions
        {
            EncryptHeaders = true,
        }, password: "pass123");

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms, password: "pass123");
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Fact]
    public void CreateArchive_WithNumThreads_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["test.txt"] = new byte[1000],
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, new SevenZipCreateOptions { NumThreads = 1 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Fact]
    public void CreateArchive_WithSolid_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["a.txt"] = System.Text.Encoding.UTF8.GetBytes("AAAA"),
            ["b.txt"] = System.Text.Encoding.UTF8.GetBytes("BBBB"),
            ["c.txt"] = System.Text.Encoding.UTF8.GetBytes("CCCC"),
        };

        using var solid = new MemoryStream();
        lib.CreateArchive("7z", solid, files, new SevenZipCreateOptions { Solid = true });

        using var nonSolid = new MemoryStream();
        lib.CreateArchive("7z", nonSolid, files, new SevenZipCreateOptions { Solid = false });

        // Both should round-trip correctly
        solid.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(solid);
        var extracted = handle.ExtractAll();
        Assert.Equal(3, extracted.Count);
    }

    [Fact]
    public void CreateArchive_GZipWithLevel_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("Hello GZip compression test data repeated many times. " + new string('x', 1000));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("gzip", ms, files, new GZipCreateOptions { Level = 9 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("gzip");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Fact]
    public void CreateArchive_XzWithLevel_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("Hello xz test " + new string('y', 1000));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("xz", ms, files, new XzCreateOptions { Level = 6, NumThreads = 1 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("xz");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Fact]
    public void CreateArchive_BZip2WithLevel_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("Hello bzip2 test " + new string('z', 1000));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("bzip2", ms, files, new BZip2CreateOptions { Level = 5 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("bzip2");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Fact]
    public void CreateArchive_BZip2WithNumPasses_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("Hello bzip2 passes test " + new string('p', 1000));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("bzip2", ms, files, new BZip2CreateOptions { NumPasses = 7 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("bzip2");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal(data, extracted.Values.First());
    }

    [Fact]
    public void CreateArchive_BrotliWithLevel_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("Hello brotli test " + new string('a', 1000));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("brotli", ms, files, new BrotliArchiveCreateOptions { Level = 9 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("brotli");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
       Assert.Equal(data, extracted.Values.First());
    }


    [Fact]
    public void CreateArchive_Lz4WithLevel_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("Hello lz4 test " + new string('b', 1000));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("lz4", ms, files, new Lz4ArchiveCreateOptions { Level = 9 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("lz4");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
       Assert.Equal(data, extracted.Values.First());
    }

    [Fact]
    public void CreateArchive_Lz4WithNumThreads_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["test.txt"] = new byte[10000],
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("lz4", ms, files, new Lz4ArchiveCreateOptions { NumThreads = 1 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("lz4");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
       Assert.Equal(files["test.txt"], extracted.Values.First());
    }

    [Fact]
    public void CreateArchive_ZipWithLevel_RoundTrips()
    {
       using var lib = ZevenLibrary.Load(DllPath);
       var data = System.Text.Encoding.UTF8.GetBytes("Hello zip test " + new string('c', 1000));
       var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

       using var ms = new MemoryStream();
       lib.CreateArchive("zip", ms, files, new ZipCreateOptions { Level = 9 });

       ms.Position = 0;
       using var handle = lib.CreateInArchive("zip");
       handle.Open(ms);
       var extracted = handle.ExtractAll();
       Assert.Single(extracted);
       Assert.Equal(data, extracted["test.txt"]);
    }

    [Fact]
    public void CreateArchive_ZipWithMethod_RoundTrips()
    {
       using var lib = ZevenLibrary.Load(DllPath);
       var data = System.Text.Encoding.UTF8.GetBytes("Hello zip deflate test " + new string('d', 1000));
       var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

       using var ms = new MemoryStream();
       lib.CreateArchive("zip", ms, files, new ZipCreateOptions { Method = "Deflate" });

       ms.Position = 0;
       using var handle = lib.CreateInArchive("zip");
       handle.Open(ms);
       var extracted = handle.ExtractAll();
       Assert.Single(extracted);
       Assert.Equal(data, extracted["test.txt"]);
    }

    [Fact]
    public void CreateArchive_XzLevelComparison()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var rng = new Random(42);
        var data = new byte[100_000];
        rng.NextBytes(data);
        var files = new Dictionary<string, byte[]> { ["data.bin"] = data };

        using var fast = new MemoryStream();
        lib.CreateArchive("xz", fast, files, new XzCreateOptions { Level = 1, NumThreads = 1 });

        using var ultra = new MemoryStream();
        lib.CreateArchive("xz", ultra, files, new XzCreateOptions { Level = 9, NumThreads = 1 });

        Assert.True(ultra.Length <= fast.Length,
            $"Level 9 ({ultra.Length}) should be <= level 1 ({fast.Length})");
    }

    [Fact]
    public void CreateArchive_ZipWithNumThreads_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("Hello zip threads " + new string('t', 1000));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("zip", ms, files, new ZipCreateOptions { NumThreads = 1 });

        ms.Position = 0;
        using var handle = lib.CreateInArchive("zip");
        handle.Open(ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal(data, extracted["test.txt"]);
    }

    [Fact]
    public void CreateArchive_StringFormat_DiskFiles_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "hello.txt"), "Hello from disk!");
            File.WriteAllBytes(Path.Combine(tempDir, "data.bin"), new byte[500]);

            var files = new Dictionary<string, string>
            {
                ["hello.txt"] = Path.Combine(tempDir, "hello.txt"),
                ["data.bin"] = Path.Combine(tempDir, "data.bin"),
            };

            using var ms = new MemoryStream();
            lib.CreateArchive("7z", ms, files);

            ms.Position = 0;
            using var handle = lib.CreateInArchive("7z");
            handle.Open(ms);
            var extracted = handle.ExtractAll();
            Assert.Equal(2, extracted.Count);
            Assert.Equal("Hello from disk!",
                System.Text.Encoding.UTF8.GetString(extracted["hello.txt"]));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateArchive_StringFormat_DiskFiles_WithOptions_RoundTrips()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "hello.txt"), "Hello with options!");
            File.WriteAllBytes(Path.Combine(tempDir, "data.bin"), new byte[500]);

            var files = new Dictionary<string, string>
            {
                ["hello.txt"] = Path.Combine(tempDir, "hello.txt"),
                ["data.bin"] = Path.Combine(tempDir, "data.bin"),
            };

            using var ms = new MemoryStream();
            lib.CreateArchive("7z", ms, files,
                new SevenZipCreateOptions { Level = 9, Method = "LZMA2" });

            ms.Position = 0;
            using var handle = lib.CreateInArchive("7z");
            handle.Open(ms);
            var extracted = handle.ExtractAll();
            Assert.Equal(2, extracted.Count);
            Assert.Equal("Hello with options!",
                System.Text.Encoding.UTF8.GetString(extracted["hello.txt"]));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}