using BenchmarkDotNet.Running;

if (args.Length > 0 && args[0] == "--ratios")
{
    CompressionRatioTable.Print();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(CodecBenchmark).Assembly).Run(args);
