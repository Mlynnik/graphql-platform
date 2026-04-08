using HotChocolate.Execution.Profiling;
using Microsoft.Extensions.DependencyInjection;

namespace HotChocolate.Execution;

/// <summary>
/// Execution profiler extensions for the <see cref="RequestContext"/>.
/// </summary>
public static class ExecutionProfilerRequestContextExtensions
{
    /// <summary>
    /// Gets execution profiler options from the current request context.
    /// </summary>
    /// <param name="context">
    /// The request context.
    /// </param>
    /// <returns>
    /// Returns profiler options.
    /// </returns>
    public static ExecutionProfilerOptions GetExecutionProfilerOptions(this RequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Schema.Services.GetRequiredService<ExecutionProfilerOptions>();
    }

    /// <summary>
    /// Gets execution profiler options from the executor.
    /// </summary>
    /// <param name="executor">
    /// The request executor.
    /// </param>
    /// <returns>
    /// Returns profiler options.
    /// </returns>
    public static ExecutionProfilerOptions GetExecutionProfilerOptions(this IRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.Schema.Services.GetRequiredService<ExecutionProfilerOptions>();
    }

    /// <summary>
    /// Gets execution profiler runtime state from the current request context.
    /// </summary>
    /// <param name="context">
    /// The request context.
    /// </param>
    /// <returns>
    /// Returns profiler runtime state.
    /// </returns>
    public static IExecutionProfilerState GetExecutionProfilerState(this RequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Schema.Services.GetRequiredService<IExecutionProfilerState>();
    }

    /// <summary>
    /// Gets execution profiler runtime state from the executor.
    /// </summary>
    /// <param name="executor">
    /// The request executor.
    /// </param>
    /// <returns>
    /// Returns profiler runtime state.
    /// </returns>
    public static IExecutionProfilerState GetExecutionProfilerState(this IRequestExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);

        return executor.Schema.Services.GetRequiredService<IExecutionProfilerState>();
    }

    /// <summary>
    /// Sets request-scoped execution profiler state override.
    /// </summary>
    /// <param name="context">
    /// The request context.
    /// </param>
    /// <param name="enabled">
    /// A value indicating whether execution profiling is enabled.
    /// </param>
    public static void SetExecutionProfilerEnabled(
        this RequestContext context,
        bool enabled)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Features.Set(new ExecutionProfilerRequestOverrides(IsEnabled: enabled));
    }

    /// <summary>
    /// Gets a value indicating whether execution profiling is enabled for the current request.
    /// </summary>
    /// <param name="context">
    /// The request context.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> if execution profiling is enabled; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsExecutionProfilerEnabled(this RequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Features.TryGet<ExecutionProfilerExecutionState>(out var executionState))
        {
            return executionState.IsEnabled;
        }

        return ResolveExecutionProfilerEnabled(context);
    }

    internal static bool ResolveExecutionProfilerEnabled(this RequestContext context)
    {
        if (context.Features.TryGet<ExecutionProfilerRequestOverrides>(out var requestOverrides)
            && requestOverrides.IsEnabled is { } requestScopedState)
        {
            return requestScopedState;
        }

        var runtimeState = context.Schema.Services.GetService<IExecutionProfilerState>();
        if (runtimeState is not null)
        {
            return runtimeState.IsEnabled;
        }

        var options = context.Schema.Services.GetService<ExecutionProfilerOptions>();
        return options?.Enabled is true;
    }

    internal static void SetExecutionProfilerExecutionState(
        this RequestContext context,
        bool enabled)
    {
        context.Features.Set(new ExecutionProfilerExecutionState(enabled));
    }
}

file sealed record ExecutionProfilerExecutionState(bool IsEnabled);
