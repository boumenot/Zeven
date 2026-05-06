using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(CodecBenchmark).Assembly).Run(args);
