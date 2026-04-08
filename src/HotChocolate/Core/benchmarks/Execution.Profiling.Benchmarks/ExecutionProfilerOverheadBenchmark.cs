using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution.Profiling;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Execution.Profiling.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net10_0)]
public class ExecutionProfilerOverheadBenchmark
{
    private const string Query = "{ greeting child { nested { name } } }";

    private IRequestExecutor _profilerOffExecutor = null!;
    private IRequestExecutor _profilerOnExecutor = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _profilerOffExecutor = await CreateExecutorAsync(enabled: false);
        _profilerOnExecutor = await CreateExecutorAsync(enabled: true);

        _ = await _profilerOffExecutor.ExecuteAsync(Query);
        _ = await _profilerOnExecutor.ExecuteAsync(Query);
    }

    [Benchmark(Baseline = true)]
    public async Task Execute_WithProfilerOff()
    {
        _ = await _profilerOffExecutor.ExecuteAsync(Query);
    }

    [Benchmark]
    public async Task Execute_WithProfilerOn()
    {
        _ = await _profilerOnExecutor.ExecuteAsync(Query);
    }

    private static ValueTask<IRequestExecutor> CreateExecutorAsync(bool enabled)
    {
        return new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<BenchmarkQuery>()
            .AddExecutionProfiler(
                options =>
                {
                    options.Enabled = enabled;
                    options.DetailLevel = ExecutionProfilerDetailLevel.SlowFields;
                    options.AggregationEnabled = false;
                    options.OpenTelemetryEnabled = false;
                    options.SlowRequestLoggingEnabled = false;
                })
            .BuildRequestExecutorAsync();
    }

    public sealed class BenchmarkQuery
    {
        public string Greeting() => "hello";

        public BenchmarkChild Child() => new();
    }

    public sealed class BenchmarkChild
    {
        public BenchmarkNested Nested() => new();
    }

    public sealed class BenchmarkNested
    {
        public string Name() => "nested";
    }
}
