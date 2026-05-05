using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Zeven.Core;

BenchmarkRunner.Run<PipeCapacityBenchmark>();

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class PipeCapacityBenchmark
{
    private byte[] data = null!;

    [Params(4096, 65536, 1024 * 1024)]
    public int PipeBufferSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ZevenLibrary.Load(@"q:\7z2601-bin\x64\7z.dll");
        this.data = new byte[256 * 1024];
        new Random(42).NextBytes(this.data);
    }

    [Benchmark(Baseline = true)]
    public byte[] Batch()
    {
        using var output = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(this.data), output, level: 1);
        return output.ToArray();
    }

    [Benchmark]
    public byte[] Stream()
    {
        using var output = new MemoryStream();
        using (var compressor = new Lzma2Stream(output, CompressionMode.Compress, level: 1,
            leaveOpen: true, pipeBufferSize: this.PipeBufferSize))
        {
            compressor.Write(this.data);
        }
        return output.ToArray();
    }
}
