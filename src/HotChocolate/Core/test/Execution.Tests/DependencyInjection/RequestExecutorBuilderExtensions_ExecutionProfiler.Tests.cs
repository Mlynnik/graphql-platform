using GreenDonut;
using GreenDonut.DependencyInjection;
using System.Diagnostics.Metrics;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution.Profiling;
using HotChocolate.Resolvers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Execution.DependencyInjection;

public class RequestExecutorBuilderExtensionsExecutionProfilerTests
{
    [Fact]
    public void AddExecutionProfiler_Should_Throw_When_BuilderIsNull()
    {
        void Fail() => RequestExecutorBuilderExtensions.AddExecutionProfiler(null!);

        Assert.Throws<ArgumentNullException>(Fail);
    }

    [Fact]
    public void AddExecutionProfilerWithConfigure_Should_Throw_When_BuilderIsNull()
    {
        void Fail() => RequestExecutorBuilderExtensions.AddExecutionProfiler(
            null!,
            _ => { });

        Assert.Throws<ArgumentNullException>(Fail);
    }

    [Fact]
    public void AddExecutionProfilerWithConfigure_Should_Throw_When_ConfigureIsNull()
    {
        void Fail() => new ServiceCollection()
            .AddGraphQLServer()
            .AddExecutionProfiler((Action<ExecutionProfilerOptions>)null!);

        Assert.Throws<ArgumentNullException>(Fail);
    }

    [Fact]
    public void AddExecutionProfilerWithConfiguration_Should_Throw_When_BuilderIsNull()
    {
        var configuration = new ConfigurationManager();

        void Fail() => RequestExecutorBuilderExtensions.AddExecutionProfiler(
            null!,
            configuration);

        Assert.Throws<ArgumentNullException>(Fail);
    }

    [Fact]
    public void AddExecutionProfilerWithConfiguration_Should_Throw_When_ConfigurationIsNull()
    {
        void Fail() => new ServiceCollection()
            .AddGraphQLServer()
            .AddExecutionProfiler((IConfiguration)null!);

        Assert.Throws<ArgumentNullException>(Fail);
    }

    [Fact]
    public void ModifyExecutionProfilerOptions_Should_Throw_When_BuilderIsNull()
    {
        void Fail() => RequestExecutorBuilderExtensions.ModifyExecutionProfilerOptions(
            null!,
            _ => { });

        Assert.Throws<ArgumentNullException>(Fail);
    }

    [Fact]
    public void ModifyExecutionProfilerOptions_Should_Throw_When_ConfigureIsNull()
    {
        void Fail() => new ServiceCollection()
            .AddGraphQLServer()
            .ModifyExecutionProfilerOptions((Action<ExecutionProfilerOptions>)null!);

        Assert.Throws<ArgumentNullException>(Fail);
    }

    [Fact]
    public async Task AddExecutionProfiler_Should_BeDisabledByDefault_When_NoConfiguration()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler());

        var options = executor.GetExecutionProfilerOptions();
        var state = executor.GetExecutionProfilerState();

        Assert.False(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.SlowFields, options.DetailLevel);
        Assert.Equal(3, options.NPlusOneListPatternThreshold);
        Assert.True(options.AggregationEnabled);
        Assert.Equal(200, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(5), options.SlidingWindowDuration);
        Assert.True(options.OpenTelemetryEnabled);
        Assert.False(options.OpenTelemetryIncludeOperationName);
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public async Task AddExecutionProfiler_Should_BindOptions_When_ConfigurationIsProvided()
    {
        var configuration = new ConfigurationManager();
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:Enabled"] = "true";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:DetailLevel"] = "NPlusOneOnly";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:NPlusOneListPatternThreshold"] = "7";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:AggregationEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowMaxRequests"] = "25";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowDuration"] = "00:02:00";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryIncludeOperationName"] = "true";

        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(configuration));

        var options = executor.GetExecutionProfilerOptions();
        var state = executor.GetExecutionProfilerState();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.NPlusOneOnly, options.DetailLevel);
        Assert.Equal(7, options.NPlusOneListPatternThreshold);
        Assert.False(options.AggregationEnabled);
        Assert.Equal(25, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(2), options.SlidingWindowDuration);
        Assert.False(options.OpenTelemetryEnabled);
        Assert.True(options.OpenTelemetryIncludeOperationName);
        Assert.True(state.IsEnabled);
    }

    [Fact]
    public async Task ModifyExecutionProfilerOptions_Should_OverrideDefaults_When_UsingFluentApi()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder
                .AddExecutionProfiler()
                .ModifyExecutionProfilerOptions(
                    options =>
                    {
                        options.Enabled = true;
                        options.DetailLevel = ExecutionProfilerDetailLevel.Full;
                        options.NPlusOneListPatternThreshold = 9;
                        options.AggregationEnabled = false;
                        options.SlidingWindowMaxRequests = 42;
                        options.SlidingWindowDuration = TimeSpan.FromSeconds(45);
                        options.OpenTelemetryEnabled = false;
                        options.OpenTelemetryIncludeOperationName = true;
                    }));

        var options = executor.GetExecutionProfilerOptions();
        var state = executor.GetExecutionProfilerState();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.Full, options.DetailLevel);
        Assert.Equal(9, options.NPlusOneListPatternThreshold);
        Assert.False(options.AggregationEnabled);
        Assert.Equal(42, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromSeconds(45), options.SlidingWindowDuration);
        Assert.False(options.OpenTelemetryEnabled);
        Assert.True(options.OpenTelemetryIncludeOperationName);
        Assert.True(state.IsEnabled);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReturnSamePayload_When_ProfilerIsDisabled()
    {
        var executorWithoutProfiler = await CreateExecutorAsync();
        var executorWithProfiler = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler());

        const string query = "{ greeting }";
        var expected = (await executorWithoutProfiler.ExecuteAsync(query)).ToJson();
        var actual = (await executorWithProfiler.ExecuteAsync(query)).ToJson();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotAddProfilingExtension_When_ProfilerIsDisabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler());

        var result = (await executor.ExecuteAsync("{ greeting }")).ExpectOperationResult();

        Assert.False(result.Extensions.ContainsKey("profiling"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_AddProfilingExtension_When_ProfilerIsEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(options => options.Enabled = true));

        var result = (await executor.ExecuteAsync("{ greeting }")).ExpectOperationResult();
        var profiling = GetProfilingExtension(result);

        Assert.True(profiling.ContainsKey("requestDurationNs"));
        Assert.True(profiling.ContainsKey("fieldCount"));
        Assert.True(profiling.ContainsKey("fields"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_RecordSyncResolverTiming_When_ProfilerIsEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(options => options.Enabled = true));

        var result = (await executor.ExecuteAsync("{ greeting }")).ExpectOperationResult();
        var field = GetFieldProfile(result, "greeting");

        Assert.Equal(1, GetFieldDepth(field));
        Assert.True(GetFieldDuration(field) >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_Should_RecordAsyncResolverTiming_When_ProfilerIsEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(options => options.Enabled = true));

        var result = (await executor.ExecuteAsync("{ asyncGreeting }")).ExpectOperationResult();
        var field = GetFieldProfile(result, "asyncGreeting");

        Assert.Equal(1, GetFieldDepth(field));
        Assert.True(GetFieldDuration(field) >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_Should_RecordFieldDepth_When_ResolverPathIsNested()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(options => options.Enabled = true));

        var result = (await executor.ExecuteAsync("{ child { nested { name } } }")).ExpectOperationResult();

        Assert.Equal(1, GetFieldDepth(GetFieldProfile(result, "child")));
        Assert.Equal(2, GetFieldDepth(GetFieldProfile(result, "child.nested")));
        Assert.Equal(3, GetFieldDepth(GetFieldProfile(result, "child.nested.name")));
    }

    [Fact]
    public async Task ExecuteAsync_Should_RecordFieldDepth_When_ResolverPathIsPureNested()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(options => options.Enabled = true));

        var result = (await executor.ExecuteAsync("{ pureChild { pureNested { pureName } } }"))
            .ExpectOperationResult();

        Assert.Equal(1, GetFieldDepth(GetFieldProfile(result, "pureChild")));
        Assert.Equal(2, GetFieldDepth(GetFieldProfile(result, "pureChild.pureNested")));
        Assert.Equal(3, GetFieldDepth(GetFieldProfile(result, "pureChild.pureNested.pureName")));
    }

    [Fact]
    public async Task ExecuteAsync_Should_RecordDataLoaderAndCacheMetrics_When_ProfilerIsEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder
                .AddExecutionProfiler(options => options.Enabled = true)
                .AddDataLoader<ProfilerDataLoader>());

        var result = (await executor.ExecuteAsync("{ a: load(key: \"a\") b: load(key: \"a\") c: load(key: \"b\") }"))
            .ExpectOperationResult();

        var profiling = GetProfilingExtension(result);
        Assert.Equal(1, GetIntValue(profiling, "dataLoaderBatchCalls"));
        Assert.Equal(1, GetIntValue(profiling, "dataLoaderCacheHits"));
        Assert.Equal(2, GetIntValue(profiling, "dataLoaderCacheMisses"));

        var bField = GetFieldProfile(result, "b");
        Assert.Equal(1, GetIntValue(bField, "dataLoaderCacheHits"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_AddNPlusOneIssue_When_RepeatedIndexedPathHasNoDataLoaderBatching()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.Full;
                    options.NPlusOneListPatternThreshold = 3;
                }));

        var result = (await executor.ExecuteAsync("{ users(count: 4) { profileName } }"))
            .ExpectOperationResult();

        var profiling = GetProfilingExtension(result);
        var nPlusOne = GetNPlusOneExtension(profiling);
        var issue = GetNPlusOneIssue(nPlusOne, "users[].profileName");

        Assert.Equal(1, GetIntValue(nPlusOne, "issueCount"));
        Assert.Equal(4, GetIntValue(issue, "occurrences"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotAddNPlusOneSection_When_DetailLevelIsSlowFields()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.SlowFields;
                    options.NPlusOneListPatternThreshold = 3;
                }));

        var result = (await executor.ExecuteAsync("{ users(count: 4) { profileName } }"))
            .ExpectOperationResult();

        var profiling = GetProfilingExtension(result);

        Assert.False(profiling.ContainsKey("nPlusOne"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_EmitOpenTelemetryMetrics_When_ProfilerMetricsEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.OpenTelemetryEnabled = true;
                    options.OpenTelemetryIncludeOperationName = true;
                }));

        long requestCount = 0;
        long fieldExecutionCount = 0;
        var requestDurations = new List<double>();
        string? capturedOperationName = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name.Equals(
                    ExecutionProfilerTelemetry.MeterName,
                    StringComparison.Ordinal))
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) =>
            {
                if (instrument.Name.Equals("graphql.execution.profiler.requests", StringComparison.Ordinal))
                {
                    requestCount += measurement;
                }
                else if (instrument.Name.Equals("graphql.execution.profiler.field.executions", StringComparison.Ordinal))
                {
                    fieldExecutionCount += measurement;
                }

                capturedOperationName ??= GetTagValue(tags, "graphql.operation.name");
            });

        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) =>
            {
                if (instrument.Name.Equals("graphql.execution.profiler.request.duration", StringComparison.Ordinal))
                {
                    requestDurations.Add(measurement);
                }

                capturedOperationName ??= GetTagValue(tags, "graphql.operation.name");
            });

        listener.Start();

        await executor.ExecuteAsync("query NamedRequest { greeting }");

        Assert.True(requestCount >= 1);
        Assert.True(fieldExecutionCount >= 1);
        Assert.NotEmpty(requestDurations);
        Assert.Equal("NamedRequest", capturedOperationName);
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotEmitOpenTelemetryMetrics_When_ProfilerMetricsDisabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.OpenTelemetryEnabled = false;
                }));

        long requestCount = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name.Equals(
                    ExecutionProfilerTelemetry.MeterName,
                    StringComparison.Ordinal))
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, _, _) =>
            {
                if (instrument.Name.Equals("graphql.execution.profiler.requests", StringComparison.Ordinal))
                {
                    requestCount += measurement;
                }
            });

        listener.Start();

        await executor.ExecuteAsync("{ greeting }");

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task ExecuteAsync_Should_AddAggregateStatisticsGroupedByOperationType_When_AggregationEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.Full;
                    options.AggregationEnabled = true;
                    options.SlidingWindowMaxRequests = 50;
                    options.SlidingWindowDuration = TimeSpan.FromMinutes(10);
                }));

        await executor.ExecuteAsync("{ greeting }");

        var result = (await executor.ExecuteAsync("mutation { updateGreeting(value: \"hello\") }"))
            .ExpectOperationResult();

        var aggregates = GetAggregatesExtension(GetProfilingExtension(result));
        var byOperationType = GetAggregateEntries(aggregates, "byOperationType");
        var queryStats = GetAggregateEntry(byOperationType, "operationType", "query");
        var mutationStats = GetAggregateEntry(byOperationType, "operationType", "mutation");

        Assert.Equal(1, GetIntValue(queryStats, "requestCount"));
        Assert.Equal(1, GetIntValue(mutationStats, "requestCount"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_EvictOldRequests_When_SlidingWindowMaxRequestsExceeded()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.Full;
                    options.AggregationEnabled = true;
                    options.SlidingWindowMaxRequests = 2;
                    options.SlidingWindowDuration = TimeSpan.FromHours(1);
                }));

        await executor.ExecuteAsync("{ greeting }");
        await executor.ExecuteAsync("{ greeting }");

        var result = (await executor.ExecuteAsync("{ greeting }")).ExpectOperationResult();

        var aggregates = GetAggregatesExtension(GetProfilingExtension(result));
        var window = GetDictionaryValue(aggregates, "window");
        var byOperationType = GetAggregateEntries(aggregates, "byOperationType");
        var queryStats = GetAggregateEntry(byOperationType, "operationType", "query");

        Assert.Equal(2, GetIntValue(window, "requestCount"));
        Assert.Equal(2, GetIntValue(queryStats, "requestCount"));
    }

    [Fact]
    public void SlidingWindowAggregator_Should_CalculatePercentiles_When_CollectingFieldDurations()
    {
        var options = new ExecutionProfilerOptions
        {
            AggregationEnabled = true,
            SlidingWindowMaxRequests = 10,
            SlidingWindowDuration = TimeSpan.FromHours(1)
        };
        var aggregator = new ExecutionProfilerSlidingWindowAggregator(options);
        var now = DateTimeOffset.UtcNow;

        for (var i = 1; i <= 5; i++)
        {
            var duration = i * 10L;
            aggregator.Add(
                new ExecutionProfilerRequestSample(
                    now.AddSeconds(i),
                    "query",
                    "PercentileProbe",
                    duration,
                    0,
                    0,
                    0,
                    [
                        new ExecutionProfilerFieldSample(
                            "Query.greeting",
                            "Query",
                            "greeting",
                            duration)
                    ]));
        }

        var snapshot = aggregator.CreateSnapshot();
        var byField = GetAggregateEntries(snapshot, "byField");
        var greetingStats = GetAggregateEntry(byField, "coordinate", "Query.greeting");
        var durationStats = GetDictionaryValue(greetingStats, "durationNs");

        Assert.Equal(10L, GetLongValue(durationStats, "minNs"));
        Assert.Equal(50L, GetLongValue(durationStats, "maxNs"));
        Assert.Equal(30.0d, GetDoubleValue(durationStats, "avgNs"), 5);
        Assert.Equal(30L, GetLongValue(durationStats, "p50Ns"));
        Assert.Equal(50L, GetLongValue(durationStats, "p95Ns"));
        Assert.Equal(50L, GetLongValue(durationStats, "p99Ns"));
    }

    [Fact]
    public void SlidingWindowAggregator_Should_EvictRequestsOutsideConfiguredDuration()
    {
        var options = new ExecutionProfilerOptions
        {
            AggregationEnabled = true,
            SlidingWindowMaxRequests = 100,
            SlidingWindowDuration = TimeSpan.FromSeconds(10)
        };
        var aggregator = new ExecutionProfilerSlidingWindowAggregator(options);
        var now = DateTimeOffset.UtcNow;

        aggregator.Add(
            new ExecutionProfilerRequestSample(
                now.AddSeconds(-20),
                "query",
                "Expired",
                5,
                0,
                0,
                0,
                [
                    new ExecutionProfilerFieldSample(
                        "Query.expired",
                        "Query",
                        "expired",
                        5)
                ]));

        aggregator.Add(
            new ExecutionProfilerRequestSample(
                now,
                "query",
                "Current",
                10,
                0,
                0,
                0,
                [
                    new ExecutionProfilerFieldSample(
                        "Query.current",
                        "Query",
                        "current",
                        10)
                ]));

        var snapshot = aggregator.CreateSnapshot();
        var window = GetDictionaryValue(snapshot, "window");
        var byOperation = GetAggregateEntries(snapshot, "byOperation");
        var currentStats = GetAggregateEntry(byOperation, "operationName", "Current");

        Assert.Equal(1, GetIntValue(window, "requestCount"));
        Assert.Equal(1, GetIntValue(currentStats, "requestCount"));
    }

    [Fact]
    public async Task IsExecutionProfilerEnabled_Should_RespectRuntimeAndRequestOverrides_When_ExecutingRequests()
    {
        var listener = new CaptureProfilerStateListener();

        var executor = await CreateExecutorAsync(
            configure: builder => builder
                .AddExecutionProfiler()
                .AddDiagnosticEventListener(_ => listener));

        await executor.ExecuteAsync("{ greeting }");

        executor.GetExecutionProfilerState().SetEnabled(enabled: true);
        await executor.ExecuteAsync("{ greeting }");

        await executor.ExecuteAsync(
            OperationRequestBuilder
                .New()
                .SetDocument("{ greeting }")
                .DisableExecutionProfiler()
                .Build());

        Assert.Equal([false, true, false], listener.CapturedStates);
    }

    [Fact]
    public void AddExecutionProfiler_Should_RegisterServices_When_CalledOnServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddExecutionProfiler();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<ExecutionProfilerOptions>();
        var state = serviceProvider.GetRequiredService<IExecutionProfilerState>();
        var aggregationStore = serviceProvider.GetRequiredService<IExecutionProfilerAggregationStore>();
        var metricsExporter = serviceProvider.GetRequiredService<IExecutionProfilerMetricsExporter>();

        Assert.False(options.Enabled);
        Assert.False(state.IsEnabled);
        Assert.NotNull(aggregationStore);
        Assert.NotNull(metricsExporter);
    }

    [Fact]
    public void ConfigureExecutionProfiler_Should_ApplyConfiguration_When_UsingServiceCollectionApi()
    {
        var configuration = new ConfigurationManager();
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:Enabled"] = "true";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:DetailLevel"] = "Full";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:AggregationEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowMaxRequests"] = "12";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowDuration"] = "00:00:30";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryIncludeOperationName"] = "true";

        var services = new ServiceCollection();
        services.ConfigureExecutionProfiler(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<ExecutionProfilerOptions>();
        var state = serviceProvider.GetRequiredService<IExecutionProfilerState>();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.Full, options.DetailLevel);
        Assert.False(options.AggregationEnabled);
        Assert.Equal(12, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromSeconds(30), options.SlidingWindowDuration);
        Assert.False(options.OpenTelemetryEnabled);
        Assert.True(options.OpenTelemetryIncludeOperationName);
        Assert.True(state.IsEnabled);
    }

    private static ValueTask<IRequestExecutor> CreateExecutorAsync(
        Action<IRequestExecutorBuilder>? configure = null)
    {
        var builder = new ServiceCollection()
            .AddGraphQLServer()
            .AddQueryType<ProfilerQuery>()
            .AddMutationType<ProfilerMutation>();

        configure?.Invoke(builder);

        return builder.BuildRequestExecutorAsync();
    }

    public sealed class ProfilerQuery
    {
        public string Greeting() => "Hello";

        public async Task<string> AsyncGreeting()
        {
            await Task.Yield();
            return "Hello async";
        }

        public ProfilerChild Child(IResolverContext context) => new();

        public PureProfilerChild PureChild() => new();

        public async Task<string?> Load(
            ProfilerDataLoader dataLoader,
            string key,
            CancellationToken cancellationToken)
            => await dataLoader.LoadAsync(key, cancellationToken);

        public IReadOnlyList<ProfilerUser> Users(int count)
        {
            var users = new List<ProfilerUser>(count);

            for (var i = 0; i < count; i++)
            {
                users.Add(new ProfilerUser($"User-{i}"));
            }

            return users;
        }
    }

    public sealed class ProfilerMutation
    {
        public string UpdateGreeting(string value) => value;
    }

    public sealed class ProfilerChild
    {
        public ProfilerNested Nested(IResolverContext context) => new();
    }

    public sealed class ProfilerNested
    {
        public string Name(IResolverContext context) => "Nested";
    }

    public sealed class PureProfilerChild
    {
        public PureProfilerNested PureNested() => new();
    }

    public sealed class PureProfilerNested
    {
        public string PureName() => "PureNested";
    }

    public sealed class ProfilerUser(string profileName)
    {
        public string ProfileName() => profileName;
    }

    public sealed class ProfilerDataLoader(
        IBatchScheduler batchScheduler,
        DataLoaderOptions options)
        : BatchDataLoader<string, string>(batchScheduler, options)
    {
        protected override Task<IReadOnlyDictionary<string, string>> LoadBatchAsync(
            IReadOnlyList<string> keys,
            CancellationToken cancellationToken)
        {
            var values = new Dictionary<string, string>(keys.Count);

            for (var i = 0; i < keys.Count; i++)
            {
                values[keys[i]] = keys[i];
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(values);
        }
    }

    private sealed class CaptureProfilerStateListener : ExecutionDiagnosticEventListener
    {
        public List<bool> CapturedStates { get; } = [];

        public override IDisposable ExecuteRequest(RequestContext context)
        {
            CapturedStates.Add(context.IsExecutionProfilerEnabled());
            return EmptyScope;
        }
    }

    private static IReadOnlyDictionary<string, object?> GetProfilingExtension(OperationResult result)
    {
        Assert.True(result.Extensions.TryGetValue("profiling", out var profilingValue));
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(profilingValue);
    }

    private static IReadOnlyDictionary<string, object?> GetAggregatesExtension(
        IReadOnlyDictionary<string, object?> profiling)
    {
        Assert.True(profiling.TryGetValue("aggregates", out var aggregates));
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(aggregates);
    }

    private static List<IReadOnlyDictionary<string, object?>> GetAggregateEntries(
        IReadOnlyDictionary<string, object?> aggregates,
        string key)
    {
        Assert.True(aggregates.TryGetValue(key, out var entriesValue));
        var entries = Assert.IsAssignableFrom<IReadOnlyList<object?>>(entriesValue);
        var result = new List<IReadOnlyDictionary<string, object?>>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            result.Add(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(entries[i]));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object?> GetAggregateEntry(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> entries,
        string key,
        string expectedValue)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].TryGetValue(key, out var keyValue)
                && keyValue is string actualValue
                && actualValue.Equals(expectedValue, StringComparison.Ordinal))
            {
                return entries[i];
            }
        }

        throw new InvalidOperationException($"Aggregate entry '{expectedValue}' for key '{key}' was not found.");
    }

    private static IReadOnlyDictionary<string, object?> GetDictionaryValue(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        Assert.True(values.TryGetValue(key, out var value));
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value);
    }

    private static IReadOnlyDictionary<string, object?> GetFieldProfile(
        OperationResult result,
        string path)
    {
        var fieldProfiles = GetFieldProfiles(result);

        for (var i = 0; i < fieldProfiles.Count; i++)
        {
            if (fieldProfiles[i].TryGetValue("path", out var pathValue)
                && pathValue is string fieldPath
                && fieldPath.Equals(path, StringComparison.Ordinal))
            {
                return fieldProfiles[i];
            }
        }

        throw new InvalidOperationException($"Field profile '{path}' was not found.");
    }

    private static List<IReadOnlyDictionary<string, object?>> GetFieldProfiles(OperationResult result)
    {
        var profiling = GetProfilingExtension(result);
        Assert.True(profiling.TryGetValue("fields", out var fieldsValue));

        var fields = Assert.IsAssignableFrom<IReadOnlyList<object?>>(fieldsValue);
        var fieldProfiles = new List<IReadOnlyDictionary<string, object?>>(fields.Count);

        for (var i = 0; i < fields.Count; i++)
        {
            fieldProfiles.Add(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(fields[i]));
        }

        return fieldProfiles;
    }

    private static int GetFieldDepth(IReadOnlyDictionary<string, object?> fieldProfile)
    {
        Assert.True(fieldProfile.TryGetValue("depth", out var depthValue));
        return Assert.IsType<int>(depthValue);
    }

    private static long GetFieldDuration(IReadOnlyDictionary<string, object?> fieldProfile)
    {
        Assert.True(fieldProfile.TryGetValue("durationNs", out var durationValue));
        return Assert.IsType<long>(durationValue);
    }

    private static int GetIntValue(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        Assert.True(values.TryGetValue(key, out var value));
        return Assert.IsType<int>(value);
    }

    private static long GetLongValue(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        Assert.True(values.TryGetValue(key, out var value));
        return Assert.IsType<long>(value);
    }

    private static double GetDoubleValue(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        Assert.True(values.TryGetValue(key, out var value));
        return Assert.IsType<double>(value);
    }

    private static string? GetTagValue(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        string key)
    {
        for (var i = 0; i < tags.Length; i++)
        {
            if (tags[i].Key.Equals(key, StringComparison.Ordinal)
                && tags[i].Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, object?> GetNPlusOneExtension(
        IReadOnlyDictionary<string, object?> profiling)
    {
        Assert.True(profiling.TryGetValue("nPlusOne", out var nPlusOne));
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(nPlusOne);
    }

    private static IReadOnlyDictionary<string, object?> GetNPlusOneIssue(
        IReadOnlyDictionary<string, object?> nPlusOne,
        string pathPattern)
    {
        Assert.True(nPlusOne.TryGetValue("issues", out var issuesValue));
        var issues = Assert.IsAssignableFrom<IReadOnlyList<object?>>(issuesValue);

        for (var i = 0; i < issues.Count; i++)
        {
            var issue = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(issues[i]);
            if (issue.TryGetValue("pathPattern", out var pathPatternValue)
                && pathPatternValue is string actualPathPattern
                && actualPathPattern.Equals(pathPattern, StringComparison.Ordinal))
            {
                return issue;
            }
        }

        throw new InvalidOperationException($"N+1 issue for '{pathPattern}' was not found.");
    }
}
