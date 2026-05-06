using System.Diagnostics;
using Zeven.Core;

public static class LeakTest
{
    private const int Iterations = 1000;

    public static void Run()
    {
        ZevenLibrary.Load(@"q:\Zeven\bin\7z.dll");

        var testData = File.ReadAllBytes(@"q:\snappy\testdata\html");
        Console.WriteLine($"Stress test: {Iterations} compress+decompress iterations per codec");
        Console.WriteLine($"Test data: html ({testData.Length:N0} bytes)");
        Console.WriteLine();
        Console.WriteLine($"{"Codec",-10} {"Start MB",10} {"End MB",10} {"Delta MB",10} {"Status"}");
        Console.WriteLine(new string('-', 52));

        RunCodec("LZMA2", testData,
            d => { var o = new MemoryStream(); Lzma2Codec.Compress(new MemoryStream(d), o); return o.ToArray(); },
            d => { var o = new MemoryStream(); Lzma2Codec.Decompress(new MemoryStream(d), o); });

        RunCodec("PPMd", testData,
            d => { var o = new MemoryStream(); PpmdCodec.Compress(new MemoryStream(d), o); return o.ToArray(); },
            d => { var o = new MemoryStream(); PpmdCodec.Decompress(new MemoryStream(d), o); });

        RunCodec("Zstd", testData,
            d => { var o = new MemoryStream(); ZstdCodec.Compress(new MemoryStream(d), o); return o.ToArray(); },
            d => { var o = new MemoryStream(); ZstdCodec.Decompress(new MemoryStream(d), o); });

        RunCodec("Brotli", testData,
            d => { var o = new MemoryStream(); BrotliCodec.Compress(new MemoryStream(d), o); return o.ToArray(); },
            d => { var o = new MemoryStream(); BrotliCodec.Decompress(new MemoryStream(d), o); });

        RunCodec("LZ4", testData,
            d => { var o = new MemoryStream(); Lz4Codec.Compress(new MemoryStream(d), o); return o.ToArray(); },
            d => { var o = new MemoryStream(); Lz4Codec.Decompress(new MemoryStream(d), o); });
    }

    private static void RunCodec(string name, byte[] data,
            Func<byte[], byte[]> compress, Action<byte[]> decompress)
    {
        // Warmup + get compressed data
        var compressed = compress(data);
        decompress(compressed);

        // Force GC to get clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        long startMem = proc.WorkingSet64;

        for (int i = 0; i < Iterations; i++)
        {
            compress(data);
            decompress(compressed);
        }

        // Force GC again to release managed objects
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        proc.Refresh();
        long endMem = proc.WorkingSet64;

        double startMb = startMem / (1024.0 * 1024);
        double endMb = endMem / (1024.0 * 1024);
        double deltaMb = (endMem - startMem) / (1024.0 * 1024);
        string status = Math.Abs(deltaMb) < 10 ? "OK" : "LEAK?";

        Console.WriteLine($"{name,-10} {startMb,10:F1} {endMb,10:F1} {deltaMb,+10:F1} {status}");
    }
}
