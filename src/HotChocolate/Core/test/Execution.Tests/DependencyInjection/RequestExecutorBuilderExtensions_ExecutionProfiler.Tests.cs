using GreenDonut;
using GreenDonut.DependencyInjection;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution.Profiling;
using HotChocolate.Resolvers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        Assert.Equal(TimeSpan.Zero, options.SlowFieldThreshold);
        Assert.Empty(options.IncludedOperationTypes);
        Assert.Empty(options.IncludedOperationNames);
        Assert.Empty(options.IncludedPathPrefixes);
        Assert.Empty(options.ExcludedObjectTypes);
        Assert.Empty(options.ExcludedFieldCoordinates);
        Assert.Empty(options.ExcludedFieldNames);
        Assert.Equal(3, options.NPlusOneListPatternThreshold);
        Assert.True(options.AggregationEnabled);
        Assert.Equal(200, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(5), options.SlidingWindowDuration);
        Assert.True(options.OpenTelemetryEnabled);
        Assert.False(options.OpenTelemetryIncludeOperationName);
        Assert.True(options.OpenTelemetryTracingEnabled);
        Assert.True(options.SlowRequestLoggingEnabled);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.SlowRequestThreshold);
        Assert.Equal(5, options.SlowRequestFieldLimit);
        Assert.False(state.IsEnabled);
    }

    [Fact]
    public async Task AddExecutionProfiler_Should_BindOptions_When_ConfigurationIsProvided()
    {
        var configuration = new ConfigurationManager();
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:Enabled"] = "true";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:DetailLevel"] = "NPlusOneOnly";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowFieldThreshold"] = "00:00:00.050";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:IncludedOperationTypes:0"] = "mutation";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:IncludedOperationNames:0"] = "NamedMutation";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:IncludedPathPrefixes:0"] = "child.nested";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:ExcludedObjectTypes:0"] = "ProfilerNested";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:ExcludedFieldCoordinates:0"] = "ProfilerQuery.greeting";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:ExcludedFieldNames:0"] = "asyncGreeting";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:NPlusOneListPatternThreshold"] = "7";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:AggregationEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowMaxRequests"] = "25";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowDuration"] = "00:02:00";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryIncludeOperationName"] = "true";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryTracingEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowRequestLoggingEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowRequestThreshold"] = "00:00:02";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowRequestFieldLimit"] = "9";

        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(configuration));

        var options = executor.GetExecutionProfilerOptions();
        var state = executor.GetExecutionProfilerState();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.NPlusOneOnly, options.DetailLevel);
        Assert.Equal(TimeSpan.FromMilliseconds(50), options.SlowFieldThreshold);
        Assert.Contains("mutation", options.IncludedOperationTypes);
        Assert.Contains("NamedMutation", options.IncludedOperationNames);
        Assert.Contains("child.nested", options.IncludedPathPrefixes);
        Assert.Contains("ProfilerNested", options.ExcludedObjectTypes);
        Assert.Contains("ProfilerQuery.greeting", options.ExcludedFieldCoordinates);
        Assert.Contains("asyncGreeting", options.ExcludedFieldNames);
        Assert.Equal(7, options.NPlusOneListPatternThreshold);
        Assert.False(options.AggregationEnabled);
        Assert.Equal(25, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(2), options.SlidingWindowDuration);
        Assert.False(options.OpenTelemetryEnabled);
        Assert.True(options.OpenTelemetryIncludeOperationName);
        Assert.False(options.OpenTelemetryTracingEnabled);
        Assert.False(options.SlowRequestLoggingEnabled);
        Assert.Equal(TimeSpan.FromSeconds(2), options.SlowRequestThreshold);
        Assert.Equal(9, options.SlowRequestFieldLimit);
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
                        options.SlowFieldThreshold = TimeSpan.FromMilliseconds(25);
                        options.IncludedOperationTypes.Add("query");
                        options.IncludedOperationNames.Add("NamedQuery");
                        options.IncludedPathPrefixes.Add("pureChild.pureNested");
                        options.ExcludedObjectTypes.Add("ProfilerNested");
                        options.ExcludedFieldCoordinates.Add("ProfilerQuery.greeting");
                        options.ExcludedFieldNames.Add("asyncGreeting");
                        options.NPlusOneListPatternThreshold = 9;
                        options.AggregationEnabled = false;
                        options.SlidingWindowMaxRequests = 42;
                        options.SlidingWindowDuration = TimeSpan.FromSeconds(45);
                        options.OpenTelemetryEnabled = false;
                        options.OpenTelemetryIncludeOperationName = true;
                        options.OpenTelemetryTracingEnabled = false;
                        options.SlowRequestLoggingEnabled = false;
                        options.SlowRequestThreshold = TimeSpan.FromSeconds(3);
                        options.SlowRequestFieldLimit = 7;
                    }));

        var options = executor.GetExecutionProfilerOptions();
        var state = executor.GetExecutionProfilerState();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.Full, options.DetailLevel);
        Assert.Equal(TimeSpan.FromMilliseconds(25), options.SlowFieldThreshold);
        Assert.Contains("query", options.IncludedOperationTypes);
        Assert.Contains("NamedQuery", options.IncludedOperationNames);
        Assert.Contains("pureChild.pureNested", options.IncludedPathPrefixes);
        Assert.Contains("ProfilerNested", options.ExcludedObjectTypes);
        Assert.Contains("ProfilerQuery.greeting", options.ExcludedFieldCoordinates);
        Assert.Contains("asyncGreeting", options.ExcludedFieldNames);
        Assert.Equal(9, options.NPlusOneListPatternThreshold);
        Assert.False(options.AggregationEnabled);
        Assert.Equal(42, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromSeconds(45), options.SlidingWindowDuration);
        Assert.False(options.OpenTelemetryEnabled);
        Assert.True(options.OpenTelemetryIncludeOperationName);
        Assert.False(options.OpenTelemetryTracingEnabled);
        Assert.False(options.SlowRequestLoggingEnabled);
        Assert.Equal(TimeSpan.FromSeconds(3), options.SlowRequestThreshold);
        Assert.Equal(7, options.SlowRequestFieldLimit);
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
    public async Task GetExecutionProfilerStatistics_Should_ReturnAggregationSnapshot_When_ProfilerIsEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.AggregationEnabled = true;
                }));

        await executor.ExecuteAsync("{ greeting }");

        var statistics = executor.GetExecutionProfilerStatistics();
        var window = GetDictionaryValue(statistics, "window");

        Assert.True(GetIntValue(window, "requestCount") >= 1);
    }

    [Fact]
    public async Task GetExecutionProfilerStatistics_Should_ReturnEmpty_When_ProfilerIsNotRegistered()
    {
        var executor = await CreateExecutorAsync();

        var statistics = executor.GetExecutionProfilerStatistics();

        Assert.Empty(statistics);
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
    public async Task ExecuteAsync_Should_RecordSerializationMetricsByType_When_ProfilerIsEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(options => options.Enabled = true));

        var result = (await executor.ExecuteAsync("{ greeting child { nested { name } } }"))
            .ExpectOperationResult();
        var profiling = GetProfilingExtension(result);
        var serializationByType = GetAggregateEntries(profiling, "serializationByType");
        var stringMetrics = GetAggregateEntry(serializationByType, "typeName", "String");

        Assert.True(GetLongValue(profiling, "serializationCount") >= 2);
        Assert.True(GetLongValue(profiling, "serializationDurationNs") >= 0);
        Assert.True(GetIntValue(stringMetrics, "count") >= 2);
        Assert.True(GetLongValue(stringMetrics, "totalDurationNs") >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ProfileOnlyConfiguredOperationType_When_OperationTypeFilterIsSet()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.IncludedOperationTypes.Add("mutation");
                }));

        var queryResult = (await executor.ExecuteAsync("{ greeting }")).ExpectOperationResult();
        var mutationResult = (await executor.ExecuteAsync("mutation { updateGreeting(value: \"hi\") }"))
            .ExpectOperationResult();

        Assert.False(HasProfilingExtension(queryResult));
        Assert.True(HasProfilingExtension(mutationResult));
    }

    [Fact]
    public async Task ExecuteAsync_Should_ProfileOnlyConfiguredOperationName_When_OperationNameFilterIsSet()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.IncludedOperationNames.Add("ProfiledQuery");
                }));

        var profiled = (await executor.ExecuteAsync("query ProfiledQuery { greeting }"))
            .ExpectOperationResult();
        var skipped = (await executor.ExecuteAsync("query SkippedQuery { greeting }"))
            .ExpectOperationResult();

        Assert.True(HasProfilingExtension(profiled));
        Assert.False(HasProfilingExtension(skipped));
    }

    [Fact]
    public async Task ExecuteAsync_Should_ProfileOnlyConfiguredPathPrefix_When_PathFilterIsSet()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.IncludedPathPrefixes.Add("child.nested");
                }));

        var profiled = (await executor.ExecuteAsync("{ greeting child { nested { name } } }"))
            .ExpectOperationResult();
        var skipped = (await executor.ExecuteAsync("{ greeting }")).ExpectOperationResult();
        var profiledPaths = GetFieldPaths(profiled);

        Assert.True(HasProfilingExtension(profiled));
        Assert.Equal(2, profiledPaths.Count);
        Assert.Contains("child.nested", profiledPaths);
        Assert.Contains("child.nested.name", profiledPaths);
        Assert.False(HasProfilingExtension(skipped));
    }

    [Fact]
    public async Task ExecuteAsync_Should_ExcludeConfiguredFieldsAndTypes_When_ExclusionsAreSet()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.ExcludedObjectTypes.Add("ProfilerNested");
                    options.ExcludedFieldCoordinates.Add("ProfilerQuery.greeting");
                    options.ExcludedFieldNames.Add("pureName");
                }));

        var result = (await executor.ExecuteAsync(
                "{ greeting child { nested { name } } pureChild { pureNested { pureName } } }"))
            .ExpectOperationResult();
        var paths = GetFieldPaths(result);

        Assert.DoesNotContain("greeting", paths);
        Assert.DoesNotContain("child.nested.name", paths);
        Assert.DoesNotContain("pureChild.pureNested.pureName", paths);
        Assert.Contains("child", paths);
        Assert.Contains("child.nested", paths);
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
    public async Task ExecuteAsync_Should_IncludeOnlyNPlusOnePayload_When_DetailLevelIsNPlusOneOnly()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.NPlusOneOnly;
                    options.NPlusOneListPatternThreshold = 3;
                }));

        var result = (await executor.ExecuteAsync("{ users(count: 4) { profileName } }"))
            .ExpectOperationResult();
        var profiling = GetProfilingExtension(result);

        Assert.True(profiling.ContainsKey("requestDurationNs"));
        Assert.True(profiling.ContainsKey("nPlusOne"));
        Assert.False(profiling.ContainsKey("fields"));
        Assert.False(profiling.ContainsKey("fieldCount"));
        Assert.False(profiling.ContainsKey("serializationByType"));
        Assert.False(profiling.ContainsKey("serializationCount"));
        Assert.False(profiling.ContainsKey("dataLoaderBatchCalls"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_IncludeOnlySlowFields_When_DetailLevelIsSlowFields()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.SlowFields;
                    options.SlowFieldThreshold = TimeSpan.FromMilliseconds(10);
                }));

        var result = (await executor.ExecuteAsync("{ greeting slowGreeting }"))
            .ExpectOperationResult();
        var paths = GetFieldPaths(result);

        Assert.Contains("slowGreeting", paths);
        Assert.DoesNotContain("greeting", paths);
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
    public async Task ExecuteAsync_Should_EmitOpenTelemetryActivity_When_ProfilerTracingEnabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.OpenTelemetryEnabled = false;
                    options.OpenTelemetryTracingEnabled = true;
                    options.OpenTelemetryIncludeOperationName = true;
                }));

        var emittedActivities = new List<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                source.Name.Equals(ExecutionProfilerTelemetry.ActivitySourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => emittedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        await executor.ExecuteAsync("query TraceableRequest { greeting }");

        var activity = Assert.Single(emittedActivities);
        Assert.Equal("graphql.execution.profile", activity.OperationName);
        Assert.Equal("query", activity.GetTagItem("graphql.operation.type"));
        Assert.Equal("TraceableRequest", activity.GetTagItem("graphql.operation.name"));
        Assert.NotNull(activity.GetTagItem("graphql.profiler.request.duration.ns"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotEmitOpenTelemetryActivity_When_ProfilerTracingDisabled()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.OpenTelemetryEnabled = false;
                    options.OpenTelemetryTracingEnabled = false;
                    options.OpenTelemetryIncludeOperationName = true;
                }));

        var emittedActivities = new List<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                source.Name.Equals(ExecutionProfilerTelemetry.ActivitySourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => emittedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        await executor.ExecuteAsync("query TraceableRequest { greeting }");

        Assert.Empty(emittedActivities);
    }

    [Fact]
    public async Task ExecuteAsync_Should_LogWarning_When_RequestIsSlowerThanConfiguredThreshold()
    {
        var logCollector = new TestLogCollectorProvider();

        var executor = await CreateExecutorAsyncWithServices(
            services =>
            {
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(logCollector);
                });
            },
            builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.SlowRequestLoggingEnabled = true;
                    options.SlowRequestThreshold = TimeSpan.FromTicks(1);
                    options.SlowRequestFieldLimit = 2;
                }));

        await executor.ExecuteAsync("{ asyncGreeting }");

        Assert.Contains(
            logCollector.Entries,
            static entry => entry.LogLevel == LogLevel.Warning
                && entry.Message.Contains("GraphQL slow request detected.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotLogWarning_When_SlowRequestLoggingIsDisabled()
    {
        var logCollector = new TestLogCollectorProvider();

        var executor = await CreateExecutorAsyncWithServices(
            services =>
            {
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(logCollector);
                });
            },
            builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.SlowRequestLoggingEnabled = false;
                    options.SlowRequestThreshold = TimeSpan.FromMilliseconds(1);
                }));

        await executor.ExecuteAsync("{ asyncGreeting }");

        Assert.DoesNotContain(
            logCollector.Entries,
            static entry => entry.LogLevel == LogLevel.Warning
                && entry.Message.Contains("GraphQL slow request detected.", StringComparison.Ordinal));
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
    public async Task ExecuteAsync_Should_HandleConcurrentRequests_When_ProfilerIsEnabled()
    {
        const int requestCount = 64;

        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.AggregationEnabled = true;
                    options.SlidingWindowMaxRequests = requestCount + 8;
                    options.SlidingWindowDuration = TimeSpan.FromMinutes(2);
                }));

        var tasks = new Task<IExecutionResult>[requestCount];
        for (var i = 0; i < requestCount; i++)
        {
            tasks[i] = executor.ExecuteAsync("{ child { nested { name } } }");
        }

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < results.Length; i++)
        {
            var operationResult = results[i].ExpectOperationResult();
            Assert.True(HasProfilingExtension(operationResult));
        }

        var statistics = executor.GetExecutionProfilerStatistics();
        var window = GetDictionaryValue(statistics, "window");

        Assert.Equal(requestCount, GetIntValue(window, "requestCount"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_KeepProfilingExtension_When_ResolverThrowsException()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.Full;
                }));

        var result = (await executor.ExecuteAsync("{ throwGreeting }")).ExpectOperationResult();
        var profiling = GetProfilingExtension(result);
        var paths = GetFieldPaths(result);

        Assert.NotEmpty(result.Errors);
        Assert.Contains("throwGreeting", paths);
        Assert.True(GetLongValue(profiling, "requestDurationNs") >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_Should_KeepProfilingExtension_When_ResolverThrowsOperationCanceledException()
    {
        var executor = await CreateExecutorAsync(
            configure: builder => builder.AddExecutionProfiler(
                options =>
                {
                    options.Enabled = true;
                    options.DetailLevel = ExecutionProfilerDetailLevel.Full;
                }));

        var result = (await executor.ExecuteAsync("{ cancelGreeting }")).ExpectOperationResult();
        var profiling = GetProfilingExtension(result);
        var paths = GetFieldPaths(result);

        Assert.NotEmpty(result.Errors);
        Assert.Contains("cancelGreeting", paths);
        Assert.True(GetLongValue(profiling, "requestDurationNs") >= 0);
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
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowFieldThreshold"] = "00:00:00.015";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:IncludedOperationTypes:0"] = "query";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:IncludedOperationNames:0"] = "ConfiguredQuery";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:IncludedPathPrefixes:0"] = "child.nested";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:ExcludedObjectTypes:0"] = "ProfilerNested";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:ExcludedFieldCoordinates:0"] = "ProfilerQuery.greeting";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:ExcludedFieldNames:0"] = "asyncGreeting";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:AggregationEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowMaxRequests"] = "12";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlidingWindowDuration"] = "00:00:30";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryIncludeOperationName"] = "true";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:OpenTelemetryTracingEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowRequestLoggingEnabled"] = "false";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowRequestThreshold"] = "00:00:01";
        configuration[$"{ExecutionProfilerOptions.DefaultConfigurationSectionPath}:SlowRequestFieldLimit"] = "4";

        var services = new ServiceCollection();
        services.ConfigureExecutionProfiler(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<ExecutionProfilerOptions>();
        var state = serviceProvider.GetRequiredService<IExecutionProfilerState>();

        Assert.True(options.Enabled);
        Assert.Equal(ExecutionProfilerDetailLevel.Full, options.DetailLevel);
        Assert.Equal(TimeSpan.FromMilliseconds(15), options.SlowFieldThreshold);
        Assert.Contains("query", options.IncludedOperationTypes);
        Assert.Contains("ConfiguredQuery", options.IncludedOperationNames);
        Assert.Contains("child.nested", options.IncludedPathPrefixes);
        Assert.Contains("ProfilerNested", options.ExcludedObjectTypes);
        Assert.Contains("ProfilerQuery.greeting", options.ExcludedFieldCoordinates);
        Assert.Contains("asyncGreeting", options.ExcludedFieldNames);
        Assert.False(options.AggregationEnabled);
        Assert.Equal(12, options.SlidingWindowMaxRequests);
        Assert.Equal(TimeSpan.FromSeconds(30), options.SlidingWindowDuration);
        Assert.False(options.OpenTelemetryEnabled);
        Assert.True(options.OpenTelemetryIncludeOperationName);
        Assert.False(options.OpenTelemetryTracingEnabled);
        Assert.False(options.SlowRequestLoggingEnabled);
        Assert.Equal(TimeSpan.FromSeconds(1), options.SlowRequestThreshold);
        Assert.Equal(4, options.SlowRequestFieldLimit);
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

    private static ValueTask<IRequestExecutor> CreateExecutorAsyncWithServices(
        Action<IServiceCollection>? configureServices = null,
        Action<IRequestExecutorBuilder>? configureBuilder = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);

        var builder = services
            .AddGraphQLServer()
            .AddQueryType<ProfilerQuery>()
            .AddMutationType<ProfilerMutation>();

        configureBuilder?.Invoke(builder);

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

        public async Task<string> SlowGreeting()
        {
            await Task.Delay(20);
            return "Slow greeting";
        }

        public string ThrowGreeting()
            => throw new InvalidOperationException("Profiler test exception");

        public string CancelGreeting()
            => throw new OperationCanceledException("Profiler test cancellation");

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

    private sealed class TestLogCollectorProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName)
            => new CollectorLogger(_entries);

        public void Dispose()
        {
        }
    }

    private sealed class CollectorLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => EmptyScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private static bool HasProfilingExtension(OperationResult result)
        => result.Extensions.ContainsKey("profiling");

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

    private static List<string> GetFieldPaths(OperationResult result)
    {
        var fieldProfiles = GetFieldProfiles(result);
        var paths = new List<string>(fieldProfiles.Count);

        for (var i = 0; i < fieldProfiles.Count; i++)
        {
            if (fieldProfiles[i].TryGetValue("path", out var value)
                && value is string path)
            {
                paths.Add(path);
            }
        }

        return paths;
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
