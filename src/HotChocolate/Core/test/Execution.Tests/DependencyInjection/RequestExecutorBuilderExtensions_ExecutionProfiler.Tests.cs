using GreenDonut;
using GreenDonut.DependencyInjection;
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
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public async Task AddExecutionProfiler_Should_BindOptions_When_ConfigurationIsProvided()
    {
        var configuration = new ConfigurationManager();
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:Enabled"] = "true";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:DetailLevel"] = "NPlusOneOnly";

        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(configuration));

        var options = executor.GetExecutionProfilerOptions();
        var state = executor.GetExecutionProfilerState();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.NPlusOneOnly, options.DetailLevel);
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
                    }));

        var options = executor.GetExecutionProfilerOptions();
        var state = executor.GetExecutionProfilerState();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.Full, options.DetailLevel);
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

        Assert.False(options.Enabled);
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public void ConfigureExecutionProfiler_Should_ApplyConfiguration_When_UsingServiceCollectionApi()
    {
        var configuration = new ConfigurationManager();
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:Enabled"] = "true";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:DetailLevel"] = "Full";

        var services = new ServiceCollection();
        services.ConfigureExecutionProfiler(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<ExecutionProfilerOptions>();
        var state = serviceProvider.GetRequiredService<IExecutionProfilerState>();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.Full, options.DetailLevel);
        Assert.True(state.IsEnabled);
    }

    private static ValueTask<IRequestExecutor> CreateExecutorAsync(
        Action<IRequestExecutorBuilder>? configure = null)
    {
        var builder = new ServiceCollection()
            .AddGraphQLServer()
            .AddQueryType<ProfilerQuery>();

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
}
