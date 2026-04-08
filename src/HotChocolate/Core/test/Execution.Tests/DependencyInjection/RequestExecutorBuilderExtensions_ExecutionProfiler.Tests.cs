using HotChocolate.Execution.Instrumentation;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution.Profiling;
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
}
