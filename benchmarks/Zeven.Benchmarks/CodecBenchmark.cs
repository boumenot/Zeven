using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows;
using Zeven.Core;
using Zeven.Core.Interop;

[MemoryDiagnoser]
[Config(typeof(NativeMemoryConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CodecBenchmark
{
    private class NativeMemoryConfig : ManualConfig
    {
        public NativeMemoryConfig()
        {
            this.AddDiagnoser(new NativeMemoryProfiler());
        }
    }
    private static readonly (string Category, string FileName)[] TestFiles =
    [
        ("html",    "html"),
        ("urls",    "urls.10K"),
        ("jpg",     "fireworks.jpeg"),
        ("pdf",     "paper-100k.pdf"),
        ("html4",   "html_x_4"),
        ("txt1",    "alice29.txt"),
        ("txt2",    "asyoulik.txt"),
        ("txt3",    "lcet10.txt"),
        ("txt4",    "plrabn12.txt"),
        ("pb",      "geo.protodata"),
        ("gaviota", "kppkn.gtb"),
    ];

    private Dictionary<string, byte[]> testData = null!;

    [GlobalSetup]
    public void Setup()
    {
        ZevenLibrary.Load(@"q:\Zeven\bin\7z.dll");
        this.testData = new Dictionary<string, byte[]>();
        foreach (var (category, fileName) in TestFiles)
        {
            this.testData[category] = File.ReadAllBytes(
                Path.Combine(@"q:\snappy\testdata", fileName));
        }
    }

    public IEnumerable<string> Categories =>
        TestFiles.Select(t => t.Category);

    // ── LZMA2 ──

    [Benchmark]
    [BenchmarkCategory("Compress")]
    [ArgumentsSource(nameof(Categories))]
    public long Lzma2(string data)
    {
        using var output = new MemoryStream();
        Lzma2Codec.Compress(new MemoryStream(this.testData[data]), output);
        return output.Length;
    }

    // ── PPMd ──

    [Benchmark]
    [BenchmarkCategory("Compress")]
    [ArgumentsSource(nameof(Categories))]
    public long Ppmd(string data)
    {
        using var output = new MemoryStream();
        PpmdCodec.Compress(new MemoryStream(this.testData[data]), output);
        return output.Length;
    }

    // ── Zstd ──

    [Benchmark]
    [BenchmarkCategory("Compress")]
    [ArgumentsSource(nameof(Categories))]
    public long Zstd(string data)
    {
        using var output = new MemoryStream();
        ZstdCodec.Compress(new MemoryStream(this.testData[data]), output);
        return output.Length;
    }

    // ── Brotli ──

    [Benchmark]
    [BenchmarkCategory("Compress")]
    [ArgumentsSource(nameof(Categories))]
    public long Brotli(string data)
    {
        using var output = new MemoryStream();
        BrotliCodec.Compress(new MemoryStream(this.testData[data]), output);
        return output.Length;
    }

    // ── LZ4 ──

    [Benchmark]
    [BenchmarkCategory("Compress")]
    [ArgumentsSource(nameof(Categories))]
    public long Lz4(string data)
    {
        using var output = new MemoryStream();
        Lz4Codec.Compress(new MemoryStream(this.testData[data]), output);
        return output.Length;
    }

    // ── Decompression benchmarks ──

    private Dictionary<string, Dictionary<string, byte[]>> compressedData = null!;

    [GlobalSetup(Target = nameof(Lzma2_Decompress))]
    public void SetupLzma2Decompress() { this.SetupDecompress("lzma2", (i, o) => Lzma2Codec.Compress(i, o)); }

    [GlobalSetup(Target = nameof(Ppmd_Decompress))]
    public void SetupPpmdDecompress() { this.SetupDecompress("ppmd", (i, o) => PpmdCodec.Compress(i, o)); }

    [GlobalSetup(Target = nameof(Zstd_Decompress))]
    public void SetupZstdDecompress() { this.SetupDecompress("zstd", (i, o) => ZstdCodec.Compress(i, o)); }

    [GlobalSetup(Target = nameof(Brotli_Decompress))]
    public void SetupBrotliDecompress() { this.SetupDecompress("brotli", (i, o) => BrotliCodec.Compress(i, o)); }

    [GlobalSetup(Target = nameof(Lz4_Decompress))]
    public void SetupLz4Decompress() { this.SetupDecompress("lz4", (i, o) => Lz4Codec.Compress(i, o)); }

    private void SetupDecompress(string codec, Action<Stream, Stream> compress)
    {
        this.Setup();
        this.compressedData ??= new Dictionary<string, Dictionary<string, byte[]>>();
        var codecData = new Dictionary<string, byte[]>();
        foreach (var (category, _) in TestFiles)
        {
            using var output = new MemoryStream();
            compress(new MemoryStream(this.testData[category]), output);
            codecData[category] = output.ToArray();
        }
        this.compressedData[codec] = codecData;
    }

    [Benchmark]
    [BenchmarkCategory("Decompress")]
    [ArgumentsSource(nameof(Categories))]
    public long Lzma2_Decompress(string data)
    {
        using var output = new MemoryStream();
        Lzma2Codec.Decompress(new MemoryStream(this.compressedData["lzma2"][data]), output);
        return output.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Decompress")]
    [ArgumentsSource(nameof(Categories))]
    public long Ppmd_Decompress(string data)
    {
        using var output = new MemoryStream();
        PpmdCodec.Decompress(new MemoryStream(this.compressedData["ppmd"][data]), output);
        return output.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Decompress")]
    [ArgumentsSource(nameof(Categories))]
    public long Zstd_Decompress(string data)
    {
        using var output = new MemoryStream();
        ZstdCodec.Decompress(new MemoryStream(this.compressedData["zstd"][data]), output);
        return output.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Decompress")]
    [ArgumentsSource(nameof(Categories))]
    public long Brotli_Decompress(string data)
    {
        using var output = new MemoryStream();
        BrotliCodec.Decompress(new MemoryStream(this.compressedData["brotli"][data]), output);
        return output.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Decompress")]
    [ArgumentsSource(nameof(Categories))]
    public long Lz4_Decompress(string data)
    {
        using var output = new MemoryStream();
        Lz4Codec.Decompress(new MemoryStream(this.compressedData["lz4"][data]), output);
        return output.Length;
    }
}
