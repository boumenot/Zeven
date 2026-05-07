using Zeven.Core;

namespace Zeven.Tests;

public class ProgressTests
{
    const string DllPath = @"q:\\Zeven\\bin\\7z.dll";

    private class TestProgress : IProgress<ArchiveProgress>
    {
        public List<ArchiveProgress> Reports { get; } = new();
        public void Report(ArchiveProgress value) => this.Reports.Add(value);
    }

    [Fact]
    public void ExtractAll_ReportsProgress()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["test.txt"] = new byte[10000] };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        var progress = new TestProgress();
        handle.ExtractAll(progress);

        Assert.NotEmpty(progress.Reports);
        Assert.True(progress.Reports[0].TotalBytes > 0);
    }

    [Fact]
    public void ExtractAll_Cancellation_Throws()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["test.txt"] = new byte[100000] };
        using var ms = new MemoryStream();
        lib.CreateArchive("7z", ms, files);

        ms.Position = 0;
        using var handle = lib.CreateInArchive("7z");
        handle.Open(ms);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel

        Assert.ThrowsAny<OperationCanceledException>(
            () => handle.ExtractAll(cancellationToken: cts.Token));
    }

    [Fact]
    public void CreateArchive_ReportsProgress()
    {
        using var lib = ZevenLibrary.Load(DllPath);
        var files = new Dictionary<string, byte[]> { ["test.txt"] = new byte[10000] };
        using var ms = new MemoryStream();

        var progress = new TestProgress();
        lib.CreateArchive("7z", ms, files, progress: progress);

        Assert.NotEmpty(progress.Reports);
    }
}
