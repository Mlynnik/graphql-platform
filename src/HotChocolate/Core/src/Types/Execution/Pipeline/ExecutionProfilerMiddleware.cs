using HotChocolate.Execution.Profiling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotChocolate.Execution.Pipeline;

internal sealed class ExecutionProfilerMiddleware
{
    private readonly RequestDelegate _next;

    private ExecutionProfilerMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async ValueTask InvokeAsync(RequestContext context)
    {
        var isEnabled = context.ResolveExecutionProfilerEnabled();
        var options = context.GetExecutionProfilerOptions();
        var aggregationStore = context.Schema.Services.GetRequiredService<IExecutionProfilerAggregationStore>();
        var metricsExporter = context.Schema.Services.GetRequiredService<IExecutionProfilerMetricsExporter>();
        var logger = context.RequestServices.GetService<ILogger<ExecutionProfilerMiddleware>>();

        // Ensure profiler state can still be resolved even when middleware is bypassed.
        if (!isEnabled)
        {
            context.SetExecutionProfilerExecutionState(enabled: false);
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (TryGetOperationIdentity(context, out var knownOperationType, out var knownOperationName)
            && !options.ShouldProfileOperation(knownOperationType, knownOperationName))
        {
            context.SetExecutionProfilerExecutionState(enabled: false);
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.SetExecutionProfilerExecutionState(enabled: true);
        var profileCollector = new ExecutionProfileCollector(options);
        context.Features.Set(profileCollector);

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            profileCollector.CompleteRequest();

            if (context.Result is OperationResult operationResult)
            {
                var operationType = GetOperationType(context);
                var operationName = GetOperationName(context);
                if (options.ShouldProfileOperation(operationType, operationName))
                {
                    var requestSample = profileCollector.CreateRequestSample(operationType, operationName);

                    if (!options.HasPathFilters || requestSample.Fields.Count > 0)
                    {
                        IReadOnlyDictionary<string, object?>? aggregates = null;

                        if (options.AggregationEnabled)
                        {
                            aggregationStore.Add(requestSample);
                            aggregates = aggregationStore.CreateSnapshot();
                        }

                        var profilingExtension = profileCollector.CreateResultExtension(options, aggregates);
                        metricsExporter.Publish(requestSample, profilingExtension);
                        LogSlowRequestIfNeeded(logger, options, requestSample);

                        operationResult.Extensions =
                            operationResult.Extensions.SetItem(
                                ExecutionProfileCollector.ExtensionKey,
                                profilingExtension);
                    }
                }
            }

            context.Features.Set<ExecutionProfileCollector>(null);
        }
    }

    public static RequestMiddlewareConfiguration Create()
        => new(
            (core, next) =>
            {
                // Ensure required services are resolvable when the pipeline is built.
                _ = core.SchemaServices.GetRequiredService<IExecutionProfilerState>();
                _ = core.SchemaServices.GetRequiredService<ExecutionProfilerOptions>();
                _ = core.SchemaServices.GetRequiredService<IExecutionProfilerAggregationStore>();
                _ = core.SchemaServices.GetRequiredService<IExecutionProfilerMetricsExporter>();

                var middleware = new ExecutionProfilerMiddleware(next);
                return context => middleware.InvokeAsync(context);
            },
            WellKnownRequestMiddleware.ExecutionProfilerMiddleware);

    private static string GetOperationType(RequestContext context)
    {
        if (context.TryGetOperation(out var operation))
        {
            return operation.Kind.ToString().ToLowerInvariant();
        }

        if (context.TryGetOperationDefinition(out var definition))
        {
            return definition.Operation.ToString().ToLowerInvariant();
        }

        return "unknown";
    }

    private static string? GetOperationName(RequestContext context)
    {
        if (context.TryGetOperation(out var operation))
        {
            return operation.Name;
        }

        if (context.TryGetOperationDefinition(out var definition))
        {
            return definition.Name?.Value;
        }

        return null;
    }

    private static bool TryGetOperationIdentity(
        RequestContext context,
        out string operationType,
        out string? operationName)
    {
        if (context.TryGetOperation(out var operation))
        {
            operationType = operation.Kind.ToString().ToLowerInvariant();
            operationName = operation.Name;
            return true;
        }

        if (context.TryGetOperationDefinition(out var definition))
        {
            operationType = definition.Operation.ToString().ToLowerInvariant();
            operationName = definition.Name?.Value;
            return true;
        }

        operationType = default!;
        operationName = null;
        return false;
    }

    private static void LogSlowRequestIfNeeded(
        ILogger<ExecutionProfilerMiddleware>? logger,
        ExecutionProfilerOptions options,
        ExecutionProfilerRequestSample requestSample)
    {
        if (logger is null
            || !options.SlowRequestLoggingEnabled
            || !logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        var thresholdNanoseconds = options.SlowRequestThreshold.Ticks * 100L;
        if (thresholdNanoseconds <= 0)
        {
            thresholdNanoseconds = 1;
        }

        if (requestSample.RequestDurationNanoseconds < thresholdNanoseconds)
        {
            return;
        }

        var fieldLimit = options.SlowRequestFieldLimit;
        if (fieldLimit <= 0)
        {
            fieldLimit = 1;
        }

        var fields = requestSample.Fields
            .OrderByDescending(static field => field.DurationNanoseconds)
            .Take(fieldLimit)
            .Select(static field =>
                $"{field.Coordinate}:{ToMilliseconds(field.DurationNanoseconds):0.###}ms");
        var slowestFields = string.Join(", ", fields);

        logger.LogWarning(
            "GraphQL slow request detected. operationType={OperationType} operationName={OperationName} "
            + "durationMs={DurationMs} fieldCount={FieldCount} dataLoaderBatchCalls={DataLoaderBatchCalls} "
            + "dataLoaderCacheHits={DataLoaderCacheHits} dataLoaderCacheMisses={DataLoaderCacheMisses} "
            + "slowestFields={SlowestFields}",
            requestSample.OperationType,
            requestSample.OperationName ?? "<anonymous>",
            ToMilliseconds(requestSample.RequestDurationNanoseconds),
            requestSample.Fields.Count,
            requestSample.DataLoaderBatchCalls,
            requestSample.DataLoaderCacheHits,
            requestSample.DataLoaderCacheMisses,
            slowestFields);
    }

    private static double ToMilliseconds(long durationNanoseconds)
        => durationNanoseconds / 1_000_000d;
}
