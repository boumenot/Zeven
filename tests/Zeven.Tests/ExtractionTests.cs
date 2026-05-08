using System.Runtime.InteropServices;
using Zeven.Core;
using Zeven.Core.Interop;

namespace Zeven.Tests;

public class ExtractionTests : IClassFixture<ArchiveFixture>
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";
    private readonly ArchiveFixture _fixture;

    public ExtractionTests(ArchiveFixture fixture) => _fixture = fixture;

    [Fact]
    public void Extract_AllFiles_ProducesCorrectContent()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var handle = lib.CreateInArchive(format.ClassId);
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        handle.Open(stream);

        var extracted = handle.ExtractAll();

        Assert.Equal(_fixture.OriginalFiles.Count, extracted.Count);
        foreach (var (name, expectedBytes) in _fixture.OriginalFiles)
        {
            // 7-Zip normalizes path separators
            var key = extracted.Keys.First(k => k.Replace('\\', '/') == name.Replace('\\', '/'));
            Assert.Equal(expectedBytes, extracted[key]);
        }
    }

    [Fact]
    public void Extract_SingleFile_ProducesCorrectContent()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var handle = lib.CreateInArchive(format.ClassId);
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        handle.Open(stream);

        // Find the index of hello.txt
        handle.Archive.GetNumberOfItems(out uint count);
        uint helloIndex = uint.MaxValue;
        for (uint i = 0; i < count; i++)
        {
            PropVariant pv = default;
            handle.Archive.GetProperty(i, PropId.kpidPath, ref pv);
            if (pv.GetBstr() == "hello.txt") helloIndex = i;
            NativeMethods.PropVariantClear(ref pv);
        }
        Assert.NotEqual(uint.MaxValue, helloIndex);

        var extracted = handle.Extract(new[] { helloIndex });

        Assert.Single(extracted);
        Assert.Equal(_fixture.OriginalFiles["hello.txt"], extracted[helloIndex]);
    }

    [Fact]
    public void Extract_TestMode_DoesNotProduceOutput()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var handle = lib.CreateInArchive(format.ClassId);
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        handle.Open(stream);

        // testMode = true means verify (no output streams)
        handle.Test();

        // If we get here without exception, the archive is valid
    }

    [Fact]
    public void Extract_UnsortedIndices_StillWorks()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["a.txt"] = System.Text.Encoding.UTF8.GetBytes("AAA"),
            ["b.txt"] = System.Text.Encoding.UTF8.GetBytes("BBB"),
            ["c.txt"] = System.Text.Encoding.UTF8.GetBytes("CCC"),
        };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        // Pass indices in reverse order
        var data = handle.Extract([2, 0]);
        Assert.Equal(2, data.Count);
        Assert.True(data.ContainsKey(2));
        Assert.True(data.ContainsKey(0));
    }

    [Fact]
    public void Extract_ByPath_ReturnsContent()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["a.txt"] = "AAA"u8.ToArray(),
            ["b.txt"] = "BBB"u8.ToArray(),
        };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        var content = handle.Extract("b.txt");
        Assert.Equal("BBB", System.Text.Encoding.UTF8.GetString(content));
    }

    [Fact]
    public void Extract_ByPath_NotFound_Throws()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["a.txt"] = "A"u8.ToArray() };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        Assert.Throws<KeyNotFoundException>(() => handle.Extract("nonexistent.txt"));
    }

    [Fact]
    public void ExtractTo_Stream_WritesContent()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["a.txt"] = "AAA"u8.ToArray(),
            ["b.txt"] = "BBB"u8.ToArray(),
        };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        using var output = new MemoryStream();
        handle.ExtractTo("b.txt", output);

        Assert.Equal("BBB", System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public void ExtractTo_Stream_NotFound_Throws()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["a.txt"] = "A"u8.ToArray() };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        Assert.Throws<KeyNotFoundException>(
            () => handle.ExtractTo("nonexistent.txt", new MemoryStream()));
    }

    [Fact]
    public void ExtractAll_ValidArchive_NoExceptions()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["test.txt"] = System.Text.Encoding.UTF8.GetBytes("hello"),
        };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        var extracted = handle.ExtractAll();
        Assert.Single(extracted);
    }

    [Fact]
    public void ExtractionResult_Enum_HasExpectedValues()
    {
        Assert.Equal(0, (int)ExtractionResult.OK);
        Assert.Equal(2, (int)ExtractionResult.DataError);
        Assert.Equal(3, (int)ExtractionResult.CrcError);
        Assert.Equal(9, (int)ExtractionResult.WrongPassword);
    }

    [Fact]
    public void ArchiveExtractionException_SingleFailure_HasMessage()
    {
        var failures = new List<ExtractionFailure> { new(0, ExtractionResult.CrcError) };
        var ex = new ArchiveExtractionException(failures);
        Assert.Contains("CrcError", ex.Message);
        Assert.Single(ex.Failures);
    }

    [Fact]
    public void ArchiveExtractionException_MultipleFailures_HasCount()
    {
        var failures = new List<ExtractionFailure>
        {
            new(0, ExtractionResult.CrcError),
            new(1, ExtractionResult.DataError),
        };
        var ex = new ArchiveExtractionException(failures);
        Assert.Contains("2 entries", ex.Message);
    }

    [Fact]
    public void Test_WrongPassword_ThrowsExtractionException()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = System.Text.Encoding.UTF8.GetBytes("classified"),
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, password: "correct");

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms, password: "wrong");

        var ex = Assert.Throws<ArchiveExtractionException>(() => handle.Test());
        Assert.NotEmpty(ex.Failures);
    }

    [Fact]
    public void ExtractAll_WrongPassword_ThrowsExtractionException()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = System.Text.Encoding.UTF8.GetBytes("classified"),
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, password: "correct");

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms, password: "wrong");

        var ex = Assert.Throws<ArchiveExtractionException>(() => handle.ExtractAll());
        Assert.NotEmpty(ex.Failures);
    }

    [Fact]
    public void Extract_WrongPassword_ThrowsExtractionException()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = System.Text.Encoding.UTF8.GetBytes("classified"),
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, password: "correct");

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms, password: "wrong");

        var ex = Assert.Throws<ArchiveExtractionException>(() => handle.Extract([0]));
        Assert.NotEmpty(ex.Failures);
    }

    [Fact]
    public void ExtractTo_WrongPassword_ThrowsExtractionException()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = System.Text.Encoding.UTF8.GetBytes("classified"),
        };

        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files, password: "correct");

        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            ms.Position = 0;
            using var handle = lib.CreateInArchive("7z");
            handle.Open(ms, password: "wrong");

            var ex = Assert.Throws<ArchiveExtractionException>(() => handle.ExtractTo(tempDir));
            Assert.NotEmpty(ex.Failures);
        }
        finally
        {
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
        }
    }

    [Fact]
    public void Extract_Cancellation_Throws()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["test.txt"] = new byte[10000] };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => handle.Extract([0], cancellationToken: cts.Token));
    }

    [Fact]
    public void ExtractTo_Cancellation_Throws()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["test.txt"] = new byte[10000] };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        var tempDir = Path.Combine(Path.GetTempPath(), "zeven_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            ms.Position = 0;
            using var handle = lib.CreateInArchive("7z");
            handle.Open(ms);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(
                () => handle.ExtractTo(tempDir, cancellationToken: cts.Token));
        }
        finally
        {
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
        }
    }

    [Fact]
    public void ExtractAll_PreCancelled_SmallArchive_Throws()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["tiny.txt"] = "x"u8.ToArray() };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => handle.ExtractAll(cancellationToken: cts.Token));
    }
}
