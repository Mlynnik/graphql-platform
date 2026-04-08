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
                operationResult.Extensions =
                    operationResult.Extensions.SetItem(
                        ExecutionProfileCollector.ExtensionKey,
                        profileCollector.CreateResultExtension(options));
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

                var middleware = new ExecutionProfilerMiddleware(next);
                return context => middleware.InvokeAsync(context);
            },
            WellKnownRequestMiddleware.ExecutionProfilerMiddleware);
}
