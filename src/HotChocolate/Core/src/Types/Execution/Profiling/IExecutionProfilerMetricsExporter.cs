namespace HotChocolate.Execution.Profiling;

internal interface IExecutionProfilerMetricsExporter
{
    void Publish(
        ExecutionProfilerRequestSample requestSample,
        IReadOnlyDictionary<string, object?> profilingExtension);
}
