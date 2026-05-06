using BenchmarkDotNet.Running;

if (args.Length > 0 && args[0] == "--ratios")
{
    CompressionRatioTable.Print();
    return;
}

if (args.Length > 0 && args[0] == "--leak-test")
{
    LeakTest.Run();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(CodecBenchmark).Assembly).Run(args);
