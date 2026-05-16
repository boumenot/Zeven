using Zeven;
using Zeven.Interop;

namespace Zeven.Tests;

public class ArchiveCreateTests
{
    static string DllPath => TestPaths.DllPath;

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
        using var inStream = new MemoryStream(archiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, inStream);

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

        using var inStream = new MemoryStream(archiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, inStream);

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

        using var inStream = new MemoryStream(archiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, inStream);

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
            using var handle = lib.OpenArchive(FormatClsid.SevenZip, archiveStream);
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
            using var handle = lib.OpenArchive(FormatClsid.SevenZip, archiveStream);

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
    public void ExtractTo_TraversalPath_BlockedEndToEnd()
    {
        // Craft a zip with a malicious "../escape.txt" entry using System.IO.Compression
        using var zipStream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(
                zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("../escape.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("escaped!");
        }

        var tempDir = Path.Combine(Path.GetTempPath(),
                "zeven_zipslip_" + Guid.NewGuid().ToString("N")[..8]);
        var extractDir = Path.Combine(tempDir, "target");
        var escapePath = Path.GetFullPath(Path.Combine(extractDir, "../escape.txt"));

        try
        {
            using var lib = ZevenLibrary.Load(DllPath);
            zipStream.Position = 0;
            using var handle = lib.OpenArchive(FormatClsid.Zip, zipStream);

            // ExtractTo must reject the traversal path
            Assert.ThrowsAny<Exception>(() => handle.ExtractTo(extractDir));

            // The escaped file must not exist
            Assert.False(File.Exists(escapePath),
                    "Zip Slip: file was written outside the target directory.");
        }
        finally
        {
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
        }
    }

    [Theory]
    [InlineData(1, null, null, null, null)]
    [InlineData(9, "LZMA2", true, 4, null)]
    [InlineData(5, "PPMd", false, 1, null)]
    [InlineData(9, null, null, null, true)]
    public void CreateArchive_7z_OptionsRoundTrip(int level, string? method,
            bool? solid, int? threads, bool? encryptHeaders)
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("test data " + new string('x', 500));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        string? password = encryptHeaders == true ? "pass123" : null;
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, new SevenZipCreateOptions
        {
            Level = level,
            Method = method,
            Solid = solid,
            NumThreads = threads,
            EncryptHeaders = encryptHeaders,
        }, password: password);

        ms.Position = 0;
        using var handle = lib.OpenArchive("7z", ms, password: password);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal(data, extracted["test.txt"]);
    }

    [Theory]
    [InlineData(1, null, null)]
    [InlineData(9, "Deflate", null)]
    [InlineData(5, "BZip2", 1)]
    [InlineData(6, null, 2)]
    public void CreateArchive_Zip_OptionsRoundTrip(int level, string? method, int? threads)
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("zip test " + new string('z', 500));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("zip", ms, files, new ZipCreateOptions
        {
            Level = level,
            Method = method,
            NumThreads = threads,
        });

        ms.Position = 0;
        using var handle = lib.OpenArchive("zip", ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
        Assert.Equal(data, extracted["test.txt"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public void CreateArchive_GZip_OptionsRoundTrip(int level)
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("gzip test " + new string('g', 500));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("gzip", ms, files, new GZipCreateOptions { Level = level });

        ms.Position = 0;
        using var handle = lib.OpenArchive("gzip", ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(5, 3)]
    [InlineData(9, 7)]
    public void CreateArchive_BZip2_OptionsRoundTrip(int level, int? numPasses)
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("bzip2 test " + new string('b', 500));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("bzip2", ms, files, new BZip2CreateOptions
        {
            Level = level,
            NumPasses = numPasses,
        });

        ms.Position = 0;
        using var handle = lib.OpenArchive("bzip2", ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(6, null)]
    [InlineData(9, 1)]
    public void CreateArchive_Xz_OptionsRoundTrip(int level, int? threads)
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("xz test " + new string('x', 500));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("xz", ms, files, new XzCreateOptions
        {
            Level = level,
            NumThreads = threads,
        });

        ms.Position = 0;
        using var handle = lib.OpenArchive("xz", ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(9)]
    public void CreateArchive_Brotli_OptionsRoundTrip(int level)
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("brotli test " + new string('r', 500));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("brotli", ms, files, new BrotliArchiveCreateOptions { Level = level });

        ms.Position = 0;
        using var handle = lib.OpenArchive("brotli", ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(9, 1)]
    [InlineData(3, null)]
    public void CreateArchive_Lz4_OptionsRoundTrip(int level, int? threads)
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var data = System.Text.Encoding.UTF8.GetBytes("lz4 test " + new string('l', 500));
        var files = new Dictionary<string, byte[]> { ["test.txt"] = data };

        using var ms = new MemoryStream();
        lib.CreateArchive("lz4", ms, files, new Lz4ArchiveCreateOptions
        {
            Level = level,
            NumThreads = threads,
        });

        ms.Position = 0;
        using var handle = lib.OpenArchive("lz4", ms);
        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
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