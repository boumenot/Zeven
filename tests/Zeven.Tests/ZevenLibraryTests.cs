using Zeven;

namespace Zeven.Tests;

public class ZevenLibraryTests
{
    static string DllPath => TestPaths.DllPath;

    [Fact]
    public void Load_ReturnsInstanceWithFormats()
    {
        using var lib = ZevenLibrary.Load(DllPath);

        Assert.NotEmpty(lib.Formats);
        Assert.Contains(lib.Formats, f => f.Name == "7z");
    }

    [Fact]
    public void CreateInArchive_ReturnsNonNull()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        using var archive = lib.CreateInArchive(format.ClassId);

        Assert.NotNull(archive);
    }

    [Fact]
    public void Load_SamePath_ReturnsSameInstance()
    {
        var lib1 = ZevenLibrary.Load(DllPath);
        var lib2 = ZevenLibrary.Load(DllPath);

        Assert.Same(lib1, lib2);
    }
}
