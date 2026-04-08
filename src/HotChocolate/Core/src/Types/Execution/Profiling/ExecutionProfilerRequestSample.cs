namespace HotChocolate.Execution.Profiling;

internal sealed record ExecutionProfilerRequestSample(
    DateTimeOffset CapturedAtUtc,
    string OperationType,
    string? OperationName,
    long RequestDurationNanoseconds,
    IReadOnlyList<ExecutionProfilerFieldSample> Fields);

internal sealed record ExecutionProfilerFieldSample(
    string Coordinate,
    string ObjectType,
    string FieldName,
    long DurationNanoseconds);
