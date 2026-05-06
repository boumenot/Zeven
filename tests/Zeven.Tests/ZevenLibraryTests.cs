using Zeven.Core;

namespace Zeven.Tests;

public class ZevenLibraryTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

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
}
