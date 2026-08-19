using BenchmarkDotNet.Running;

// Benchmark suites will be added as the mediator pipeline is implemented.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
