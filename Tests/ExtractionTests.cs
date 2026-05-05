using System.Runtime.InteropServices;
using SevenZipNet;
using SevenZipNet.Interop;

namespace SevenZipNet.Tests;

public class ExtractionTests : IClassFixture<ArchiveFixture>
{
    const string DllPath = @"q:\7z2601-bin\x64\7z.dll";
    private readonly ArchiveFixture _fixture;

    public ExtractionTests(ArchiveFixture fixture) => _fixture = fixture;

    [Fact]
    public void Extract_AllFiles_ProducesCorrectContent()
    {
        using var lib = new SevenZipLibrary(DllPath);
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
        using var lib = new SevenZipLibrary(DllPath);
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
        using var lib = new SevenZipLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");
        using var handle = lib.CreateInArchive(format.ClassId);
        using var stream = new MemoryStream(_fixture.ArchiveBytes);
        handle.Open(stream);

        // testMode = true means verify (no output streams)
        handle.Test();

        // If we get here without exception, the archive is valid
    }
}
