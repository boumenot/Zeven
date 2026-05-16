using Zeven;

namespace Zeven.Tests;

public class DisposableTests
{
    static string DllPath => TestPaths.DllPath;

    [Fact]
    public void ArchiveHandle_CanBeUsedInUsingBlock()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        // Create a small archive
        byte[] archiveBytes;
        using (var ms = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, ms, new Dictionary<string, byte[]> { ["a.txt"] = "A"u8.ToArray() });
            archiveBytes = ms.ToArray();
        }

        // Open, extract, dispose — all within using scope
        Dictionary<string, byte[]> extracted;
        using (var handle = lib.OpenArchive(format.ClassId, new MemoryStream(archiveBytes)))
        {
            extracted = handle.ExtractAll();
        }
        // After dispose, extracted data is still accessible
        Assert.Equal("A"u8.ToArray(), extracted["a.txt"]);
    }

    [Fact]
    public void MultipleArchives_CanOpenSimultaneously()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        byte[] archive1, archive2;
        using (var ms = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, ms, new Dictionary<string, byte[]> { ["one.txt"] = "1"u8.ToArray() });
            archive1 = ms.ToArray();
        }
        using (var ms = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, ms, new Dictionary<string, byte[]> { ["two.txt"] = "2"u8.ToArray() });
            archive2 = ms.ToArray();
        }

        using var h1 = lib.OpenArchive(format.ClassId, new MemoryStream(archive1));
        using var h2 = lib.OpenArchive(format.ClassId, new MemoryStream(archive2));

        var e1 = h1.ExtractAll();
        var e2 = h2.ExtractAll();

        Assert.Equal("1"u8.ToArray(), e1["one.txt"]);
        Assert.Equal("2"u8.ToArray(), e2["two.txt"]);
    }

    [Fact]
    public void ZevenLibrary_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ZevenLibrary)));
    }

    [Fact]
    public void ArchiveHandle_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ArchiveHandle)));
    }

    /// <summary>
    /// Regression test for COM pointer leak in CreateInArchive.
    /// Before the fix, each CreateInArchive call leaked the native COM pointer
    /// from createObject — refcount never reached 0.
    /// At 100K iterations, the leak produces ~100 MB growth.
    /// </summary>
    [Fact]
    [Trait("Category", "Stress")]
    public void CreateAndDisposeArchive_RepeatedlyDoesNotLeakMemory()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        // Use larger data so each leaked COM object holds meaningful memory
        var data = new byte[256 * 1024];
        new Random(42).NextBytes(data);
        var files = new Dictionary<string, byte[]> { ["data.bin"] = data };

        using var archiveData = new MemoryStream();
        lib.CreateArchive("7z", archiveData, files);
        var archiveBytes = archiveData.ToArray();

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            using var handle = lib.OpenArchive("7z", new MemoryStream(archiveBytes));
            handle.ExtractAll();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long baseline = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;

        for (int i = 0; i < 100_000; i++)
        {
            using var handle = lib.OpenArchive("7z", new MemoryStream(archiveBytes));
            handle.ExtractAll();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long after = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;

        long deltaMb = (after - baseline) / (1024 * 1024);
        Assert.True(deltaMb < 50, $"Memory grew by {deltaMb} MB — possible COM ref leak");
    }
}
