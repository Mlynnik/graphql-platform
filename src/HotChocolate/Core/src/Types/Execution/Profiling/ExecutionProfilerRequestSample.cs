namespace HotChocolate.Execution.Profiling;

internal sealed record ExecutionProfilerRequestSample(
    DateTimeOffset CapturedAtUtc,
    string OperationType,
    string? OperationName,
    long RequestDurationNanoseconds,
    long DataLoaderBatchCalls,
    long DataLoaderCacheHits,
    long DataLoaderCacheMisses,
    IReadOnlyList<ExecutionProfilerFieldSample> Fields);

internal sealed record ExecutionProfilerFieldSample(
    string Coordinate,
    string ObjectType,
    string FieldName,
    long DurationNanoseconds);
