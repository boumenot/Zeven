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

if (args.Length > 0 && args[0] == "--leak-test-large")
{
    LeakTest.Run(large: true);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(CodecBenchmark).Assembly).Run(args);
