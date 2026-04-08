namespace HotChocolate.Execution.Profiling;

/// <summary>
/// Provides OpenTelemetry integration constants for the execution profiler.
/// </summary>
public static class ExecutionProfilerTelemetry
{
    /// <summary>
    /// The OpenTelemetry meter name used by the execution profiler.
    /// </summary>
    public const string MeterName = "HotChocolate.Execution.Profiler";

    /// <summary>
    /// The OpenTelemetry activity source name used by the execution profiler.
    /// </summary>
    public const string ActivitySourceName = "HotChocolate.Execution.Profiler";
}
