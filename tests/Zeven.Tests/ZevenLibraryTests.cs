using Zeven;

namespace Zeven.Tests;

public class ZevenLibraryTests
{
    const string DllPath = @"q:\7z2601-bin\x64\7z.dll";

    [Fact]
    public void Load_ReturnsInstanceWithFormats()
    {
        using var lib = new ZevenLibrary(DllPath);

        Assert.NotEmpty(lib.Formats);
        Assert.Contains(lib.Formats, f => f.Name == "7z");
    }

    [Fact]
    public void CreateInArchive_ReturnsNonNull()
    {
        using var lib = new ZevenLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        using var archive = lib.CreateInArchive(format.ClassId);

        Assert.NotNull(archive);
    }
}
