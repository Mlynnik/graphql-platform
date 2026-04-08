namespace HotChocolate.Execution;

/// <summary>
/// Provides extension methods for execution profiler request overrides.
/// </summary>
public static class ExecutionProfilerRequestOverridesExtensions
{
    /// <summary>
    /// Enables execution profiling for the current request.
    /// </summary>
    public static OperationRequestBuilder EnableExecutionProfiler(
        this OperationRequestBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.SetExecutionProfilerEnabled(enabled: true);
    }

    /// <summary>
    /// Disables execution profiling for the current request.
    /// </summary>
    public static OperationRequestBuilder DisableExecutionProfiler(
        this OperationRequestBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.SetExecutionProfilerEnabled(enabled: false);
    }

    /// <summary>
    /// Sets execution profiling state for the current request.
    /// </summary>
    /// <param name="builder">
    /// The operation request builder.
    /// </param>
    /// <param name="enabled">
    /// A value indicating whether execution profiling is enabled.
    /// </param>
    /// <returns>
    /// Returns the operation request builder.
    /// </returns>
    public static OperationRequestBuilder SetExecutionProfilerEnabled(
        this OperationRequestBuilder builder,
        bool enabled)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Features.Get<ExecutionProfilerRequestOverrides>();

        if (options is null)
        {
            options = new ExecutionProfilerRequestOverrides(IsEnabled: enabled);
        }
        else
        {
            options = options with { IsEnabled = enabled };
        }

        builder.Features.Set(options);
        return builder;
    }
}
