namespace HotChocolate.Execution.Profiling;

internal interface IExecutionProfilerAggregationStore
{
    void Add(ExecutionProfilerRequestSample sample);

    IReadOnlyDictionary<string, object?> CreateSnapshot();
}
