using SevenZipNet;

namespace SevenZipNet.Tests;

public class DisposableTests
{
    const string DllPath = @"q:\7z2601-bin\x64\7za.dll";

    [Fact]
    public void ArchiveHandle_CanBeUsedInUsingBlock()
    {
        using var lib = new SevenZipLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        // Create a small archive
        byte[] archiveBytes;
        using (var ms = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, ms, new() { ["a.txt"] = "A"u8.ToArray() });
            archiveBytes = ms.ToArray();
        }

        // Open, extract, dispose — all within using scope
        Dictionary<string, byte[]> extracted;
        using (var handle = lib.CreateInArchive(format.ClassId))
        {
            handle.Open(new MemoryStream(archiveBytes));
            extracted = handle.ExtractAll();
        }
        // After dispose, extracted data is still accessible
        Assert.Equal("A"u8.ToArray(), extracted["a.txt"]);
    }

    [Fact]
    public void MultipleArchives_CanOpenSimultaneously()
    {
        using var lib = new SevenZipLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        byte[] archive1, archive2;
        using (var ms = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, ms, new() { ["one.txt"] = "1"u8.ToArray() });
            archive1 = ms.ToArray();
        }
        using (var ms = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, ms, new() { ["two.txt"] = "2"u8.ToArray() });
            archive2 = ms.ToArray();
        }

        using var h1 = lib.CreateInArchive(format.ClassId);
        using var h2 = lib.CreateInArchive(format.ClassId);
        h1.Open(new MemoryStream(archive1));
        h2.Open(new MemoryStream(archive2));

        var e1 = h1.ExtractAll();
        var e2 = h2.ExtractAll();

        Assert.Equal("1"u8.ToArray(), e1["one.txt"]);
        Assert.Equal("2"u8.ToArray(), e2["two.txt"]);
    }

    [Fact]
    public void SevenZipLibrary_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(SevenZipLibrary)));
    }

    [Fact]
    public void ArchiveHandle_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ArchiveHandle)));
    }
}
