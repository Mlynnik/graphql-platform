using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HotChocolate.Execution.Profiling;

internal sealed class ExecutionProfilerOpenTelemetryExporter : IExecutionProfilerMetricsExporter
{
    private static readonly Meter s_meter = new(
        ExecutionProfilerTelemetry.MeterName,
        typeof(ExecutionProfilerOpenTelemetryExporter).Assembly.GetName().Version?.ToString());

    private static readonly Counter<long> s_requestCounter =
        s_meter.CreateCounter<long>(
            "graphql.execution.profiler.requests",
            description: "Total number of profiled GraphQL requests.");

    private static readonly Histogram<double> s_requestDurationHistogram =
        s_meter.CreateHistogram<double>(
            "graphql.execution.profiler.request.duration",
            "ms",
            "Duration of profiled GraphQL requests.");

    private static readonly Counter<long> s_fieldExecutionCounter =
        s_meter.CreateCounter<long>(
            "graphql.execution.profiler.field.executions",
            description: "Total number of profiled GraphQL field executions.");

    private static readonly Histogram<double> s_fieldDurationHistogram =
        s_meter.CreateHistogram<double>(
            "graphql.execution.profiler.field.duration",
            "ms",
            "Duration of profiled GraphQL field resolver executions.");

    private static readonly Counter<long> s_dataLoaderBatchCounter =
        s_meter.CreateCounter<long>(
            "graphql.execution.profiler.dataloader.batches",
            description: "Total number of DataLoader batch executions.");

    private static readonly Counter<long> s_dataLoaderCacheHitCounter =
        s_meter.CreateCounter<long>(
            "graphql.execution.profiler.dataloader.cache_hits",
            description: "Total number of DataLoader cache hits.");

    private static readonly Counter<long> s_dataLoaderCacheMissCounter =
        s_meter.CreateCounter<long>(
            "graphql.execution.profiler.dataloader.cache_misses",
            description: "Total number of DataLoader cache misses.");

    private static readonly Counter<long> s_nPlusOneIssueCounter =
        s_meter.CreateCounter<long>(
            "graphql.execution.profiler.nplusone.issues",
            description: "Total number of detected potential N+1 issues.");

    private readonly ExecutionProfilerOptions _options;

    public ExecutionProfilerOpenTelemetryExporter(ExecutionProfilerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Publish(
        ExecutionProfilerRequestSample requestSample,
        IReadOnlyDictionary<string, object?> profilingExtension)
    {
        ArgumentNullException.ThrowIfNull(requestSample);
        ArgumentNullException.ThrowIfNull(profilingExtension);

        if (!_options.OpenTelemetryEnabled)
        {
            return;
        }

        var tags = CreateOperationTags(requestSample.OperationType, requestSample.OperationName);

        s_requestCounter.Add(1, tags);
        s_requestDurationHistogram.Record(ToMilliseconds(requestSample.RequestDurationNanoseconds), tags);

        s_dataLoaderBatchCounter.Add(requestSample.DataLoaderBatchCalls, tags);
        s_dataLoaderCacheHitCounter.Add(requestSample.DataLoaderCacheHits, tags);
        s_dataLoaderCacheMissCounter.Add(requestSample.DataLoaderCacheMisses, tags);

        for (var i = 0; i < requestSample.Fields.Count; i++)
        {
            var field = requestSample.Fields[i];
            var fieldTags = tags;
            fieldTags.Add("graphql.field.coordinate", field.Coordinate);
            fieldTags.Add("graphql.field.object_type", field.ObjectType);
            fieldTags.Add("graphql.field.name", field.FieldName);

            s_fieldExecutionCounter.Add(1, fieldTags);
            s_fieldDurationHistogram.Record(ToMilliseconds(field.DurationNanoseconds), fieldTags);
        }

        var nPlusOneIssueCount = TryGetNPlusOneIssueCount(profilingExtension);
        if (nPlusOneIssueCount > 0)
        {
            s_nPlusOneIssueCounter.Add(nPlusOneIssueCount, tags);
        }
    }

    private TagList CreateOperationTags(
        string operationType,
        string? operationName)
    {
        var tags = new TagList
        {
            { "graphql.operation.type", operationType }
        };

        if (_options.OpenTelemetryIncludeOperationName
            && !string.IsNullOrWhiteSpace(operationName))
        {
            tags.Add("graphql.operation.name", operationName);
        }

        return tags;
    }

    private static long TryGetNPlusOneIssueCount(IReadOnlyDictionary<string, object?> profilingExtension)
    {
        if (!profilingExtension.TryGetValue("nPlusOne", out var nPlusOneValue))
        {
            return 0;
        }

        if (nPlusOneValue is not IReadOnlyDictionary<string, object?> nPlusOne)
        {
            return 0;
        }

        if (!nPlusOne.TryGetValue("issueCount", out var issueCount))
        {
            return 0;
        }

        return issueCount switch
        {
            int value when value > 0 => value,
            long value when value > 0 => value,
            _ => 0
        };
    }

    private static double ToMilliseconds(long durationNanoseconds)
        => durationNanoseconds / 1_000_000d;
}
