using SevenZipNet;

namespace SevenZipNet.Tests;

public class SevenZipLibraryTests
{
    const string DllPath = @"q:\7z2601-bin\x64\7za.dll";

    [Fact]
    public void Load_ReturnsInstanceWithFormats()
    {
        using var lib = new SevenZipLibrary(DllPath);

        Assert.NotEmpty(lib.Formats);
        Assert.Contains(lib.Formats, f => f.Name == "7z");
    }

    [Fact]
    public void CreateInArchive_ReturnsNonNull()
    {
        using var lib = new SevenZipLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        using var archive = lib.CreateInArchive(format.ClassId);

        Assert.NotNull(archive);
    }
}
