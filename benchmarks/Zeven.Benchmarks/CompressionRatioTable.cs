using System.Diagnostics;
using Zeven.Core;
using Zeven.Core.Interop;

public static class CompressionRatioTable
{
    private const int Runs = 10;

    public static void Print()
    {
        ZevenLibrary.Load(@"q:\Zeven\bin\7z.dll");

        var files = new (string Cat, string File)[] {
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
        };

        var codecs = new (string Name, Action<Stream,Stream> Compress)[] {
            ("LZMA2",  (i,o) => Lzma2Codec.Compress(i,o)),
            ("PPMd",   (i,o) => PpmdCodec.Compress(i,o)),
            ("Zstd",   (i,o) => ZstdCodec.Compress(i,o)),
            ("Brotli", (i,o) => BrotliCodec.Compress(i,o)),
            ("LZ4",    (i,o) => Lz4Codec.Compress(i,o)),
        };

        // Warmup
        foreach (var (_, compress) in codecs)
        {
            using var ms = new MemoryStream();
            compress(new MemoryStream(new byte[100]), ms);
        }

        Console.WriteLine("| Dataset  | Original |      LZMA2      |      PPMd       |      Zstd       |     Brotli      |       LZ4       |");
        Console.WriteLine("|----------|----------|-----------------|-----------------|-----------------|-----------------|-----------------|");

        foreach (var (cat, file) in files)
        {
            var data = File.ReadAllBytes(Path.Combine(@"q:\snappy\testdata", file));
            var parts = new List<string>();
            foreach (var (name, compress) in codecs)
            {
                // Measure ratio on first run
                long compressedLen;
                using (var ms = new MemoryStream())
                {
                    compress(new MemoryStream(data), ms);
                    compressedLen = ms.Length;
                }
                double ratio = (double)compressedLen / data.Length * 100;

                // Measure average time over Runs iterations
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < Runs; i++)
                {
                    using var ms = new MemoryStream();
                    compress(new MemoryStream(data), ms);
                }
                sw.Stop();
                double avgMs = sw.Elapsed.TotalMilliseconds / Runs;

                parts.Add($"{ratio,5:F1}% ({FormatTime(avgMs)})");
            }
            Console.WriteLine($"| {cat,-8} | {data.Length,8:N0} | {string.Join(" | ", parts)} |");
        }
    }

    private static string FormatTime(double ms)
    {
        if (ms >= 1000)
        {
            return $"{ms / 1000,5:F2}s";
        }
        if (ms >= 1)
        {
            return $"{ms,4:F1}ms";
        }
        return $"{ms * 1000,4:F0}µs";
    }
}
