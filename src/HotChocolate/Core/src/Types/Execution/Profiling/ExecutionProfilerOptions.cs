namespace HotChocolate.Execution.Profiling;

/// <summary>
/// Represents options for GraphQL execution profiling.
/// </summary>
public sealed class ExecutionProfilerOptions
{
    /// <summary>
    /// The default configuration section path.
    /// </summary>
    public const string DefaultConfigurationSectionPath = "HotChocolate:Execution:Profiler";

    /// <summary>
    /// Gets or sets a value indicating whether profiling is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the profiling detail level.
    /// </summary>
    public ExecutionProfilerDetailLevel DetailLevel { get; set; } = ExecutionProfilerDetailLevel.SlowFields;

    /// <summary>
    /// Gets operation types to include in profiling. Empty set means all operation types.
    /// </summary>
    public ISet<string> IncludedOperationTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets operation names to include in profiling. Empty set means all operation names.
    /// </summary>
    public ISet<string> IncludedOperationNames { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets field path prefixes to include in profiling. Empty set means all paths.
    /// </summary>
    public ISet<string> IncludedPathPrefixes { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets object types that should be excluded from field-level profiling.
    /// </summary>
    public ISet<string> ExcludedObjectTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets field coordinates that should be excluded from field-level profiling.
    /// </summary>
    public ISet<string> ExcludedFieldCoordinates { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets field names that should be excluded from field-level profiling.
    /// </summary>
    public ISet<string> ExcludedFieldNames { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the minimum number of indexed path occurrences that triggers N+1 detection.
    /// </summary>
    public int NPlusOneListPatternThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether sliding-window aggregation is enabled.
    /// </summary>
    public bool AggregationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of profiled requests retained in the aggregation window.
    /// </summary>
    public int SlidingWindowMaxRequests { get; set; } = 200;

    /// <summary>
    /// Gets or sets the time span retained in the aggregation window.
    /// </summary>
    public TimeSpan SlidingWindowDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether profiler metrics should be emitted via OpenTelemetry meters.
    /// </summary>
    public bool OpenTelemetryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether operation names should be included in OpenTelemetry metric tags.
    /// </summary>
    public bool OpenTelemetryIncludeOperationName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether slow request logging is enabled.
    /// </summary>
    public bool SlowRequestLoggingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the slow request threshold for warning logs.
    /// </summary>
    public TimeSpan SlowRequestThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the maximum number of slowest fields included in a slow request log entry.
    /// </summary>
    public int SlowRequestFieldLimit { get; set; } = 5;

    internal bool HasPathFilters => IncludedPathPrefixes.Count > 0;

    internal bool ShouldProfileOperation(
        string operationType,
        string? operationName)
    {
        if (IncludedOperationTypes.Count > 0
            && !IncludedOperationTypes.Contains(operationType))
        {
            return false;
        }

        if (IncludedOperationNames.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                return false;
            }

            if (!IncludedOperationNames.Contains(operationName))
            {
                return false;
            }
        }

        return true;
    }

    internal bool ShouldProfileField(
        string path,
        string coordinate,
        string objectType,
        string fieldName)
    {
        if (!ShouldProfilePath(path))
        {
            return false;
        }

        if (ExcludedObjectTypes.Contains(objectType)
            || ExcludedFieldCoordinates.Contains(coordinate)
            || ExcludedFieldNames.Contains(fieldName))
        {
            return false;
        }

        return true;
    }

    internal bool ShouldProfilePath(string path)
    {
        if (!HasPathFilters)
        {
            return true;
        }

        var normalizedPath = NormalizeIndexedPath(path);
        return MatchesPathPrefix(path, normalizedPath);
    }

    private bool MatchesPathPrefix(
        string path,
        string normalizedPath)
    {
        foreach (var prefix in IncludedPathPrefixes)
        {
            if (IsPathPrefix(path, prefix) || IsPathPrefix(normalizedPath, prefix))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPathPrefix(
        string path,
        string prefix)
    {
        if (path.Equals(prefix, StringComparison.Ordinal))
        {
            return true;
        }

        if (!path.StartsWith(prefix, StringComparison.Ordinal)
            || path.Length <= prefix.Length)
        {
            return false;
        }

        var delimiter = path[prefix.Length];
        return delimiter is '.' or '[';
    }

    private static string NormalizeIndexedPath(string path)
    {
        if (path.Length == 0)
        {
            return path;
        }

        var normalized = new System.Text.StringBuilder(path.Length);

        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '[')
            {
                normalized.Append("[]");
                i++;

                while (i < path.Length && path[i] != ']')
                {
                    i++;
                }

                continue;
            }

            normalized.Append(path[i]);
        }

        return normalized.ToString();
    }
}
