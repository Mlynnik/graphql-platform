using HotChocolate.Execution.Profiling;
using Microsoft.Extensions.DependencyInjection;

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

        // Ensure profiler state can still be resolved even when middleware is bypassed.
        if (!isEnabled)
        {
            context.SetExecutionProfilerExecutionState(enabled: false);
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.SetExecutionProfilerExecutionState(enabled: true);
        var profileCollector = new ExecutionProfileCollector();
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
                var requestSample = profileCollector.CreateRequestSample(operationType, operationName);

                IReadOnlyDictionary<string, object?>? aggregates = null;

                if (options.AggregationEnabled)
                {
                    aggregationStore.Add(requestSample);
                    aggregates = aggregationStore.CreateSnapshot();
                }

                var profilingExtension = profileCollector.CreateResultExtension(options, aggregates);
                metricsExporter.Publish(requestSample, profilingExtension);

                operationResult.Extensions =
                    operationResult.Extensions.SetItem(
                        ExecutionProfileCollector.ExtensionKey,
                        profilingExtension);
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
}
