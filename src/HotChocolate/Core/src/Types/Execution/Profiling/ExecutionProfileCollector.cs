using System.Diagnostics;
using System.Text;
using System.Threading;

namespace HotChocolate.Execution.Profiling;

internal sealed class ExecutionProfileCollector
{
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private readonly List<ExecutionProfileFieldEntry> _fields = [];
    private readonly Dictionary<string, ExecutionProfileFieldMetrics> _fieldMetrics = [];
    private readonly long _requestStartTimestamp = Stopwatch.GetTimestamp();
    private long _requestDurationNanoseconds;
    private int _dataLoaderBatchCalls;
    private int _dataLoaderCacheHits;
    private int _dataLoaderCacheMisses;

    public const string ExtensionKey = "profiling";

    public void AddField(
        Path path,
        long durationNanoseconds,
        string coordinate,
        string objectType,
        string fieldName)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(coordinate);
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(fieldName);

        if (durationNanoseconds < 0)
        {
            durationNanoseconds = 0;
        }

        var pathValue = path.Print();

        if (coordinate.Length == 0)
        {
            coordinate = pathValue;
        }

        if (objectType.Length == 0)
        {
            objectType = "Unknown";
        }

        if (fieldName.Length == 0)
        {
            fieldName = pathValue;
        }

        var field = new ExecutionProfileFieldEntry(
            pathValue,
            GetFieldDepth(path),
            durationNanoseconds,
            coordinate,
            objectType,
            fieldName);

        lock (_sync)
        {
            _fields.Add(field);
        }
    }

    public void AddDataLoaderBatch(Path? path, int keyCount)
    {
        if (keyCount < 0)
        {
            keyCount = 0;
        }

        lock (_sync)
        {
            _dataLoaderBatchCalls++;
            _dataLoaderCacheMisses += keyCount;

            if (path is not null)
            {
                var metrics = GetOrCreateFieldMetrics(path.Print());
                metrics.DataLoaderBatchCalls++;
                metrics.DataLoaderCacheMisses += keyCount;
            }
        }
    }

    public void AddDataLoaderCacheHit(Path? path)
    {
        lock (_sync)
        {
            _dataLoaderCacheHits++;

            if (path is not null)
            {
                var metrics = GetOrCreateFieldMetrics(path.Print());
                metrics.DataLoaderCacheHits++;
            }
        }
    }

    public void CompleteRequest()
    {
        Volatile.Write(ref _requestDurationNanoseconds, GetElapsedNanoseconds(_requestStartTimestamp));
    }

    public IReadOnlyDictionary<string, object?> CreateResultExtension(
        ExecutionProfilerOptions options,
        IReadOnlyDictionary<string, object?>? aggregates = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var snapshot = CaptureSnapshot();

        var serializedFields = new object?[snapshot.Fields.Length];

        for (var i = 0; i < snapshot.Fields.Length; i++)
        {
            var field = snapshot.Fields[i];
            snapshot.FieldMetrics.TryGetValue(field.Path, out var fieldMetric);
            serializedFields[i] = new Dictionary<string, object?>
            {
                ["path"] = field.Path,
                ["depth"] = field.Depth,
                ["durationNs"] = field.DurationNanoseconds,
                ["coordinate"] = field.Coordinate,
                ["objectType"] = field.ObjectType,
                ["fieldName"] = field.FieldName,
                ["dataLoaderBatchCalls"] = fieldMetric?.DataLoaderBatchCalls ?? 0,
                ["dataLoaderCacheHits"] = fieldMetric?.DataLoaderCacheHits ?? 0,
                ["dataLoaderCacheMisses"] = fieldMetric?.DataLoaderCacheMisses ?? 0
            };
        }

        var result = new Dictionary<string, object?>
        {
            ["requestDurationNs"] = snapshot.RequestDurationNanoseconds,
            ["fieldCount"] = snapshot.Fields.Length,
            ["dataLoaderBatchCalls"] = snapshot.DataLoaderBatchCalls,
            ["dataLoaderCacheHits"] = snapshot.DataLoaderCacheHits,
            ["dataLoaderCacheMisses"] = snapshot.DataLoaderCacheMisses,
            ["fields"] = serializedFields
        };

        if (aggregates is { Count: > 0 })
        {
            result["aggregates"] = aggregates;
        }

        if (options.DetailLevel is ExecutionProfilerDetailLevel.Full
            or ExecutionProfilerDetailLevel.NPlusOneOnly)
        {
            var nPlusOneIssues = AnalyzeNPlusOneIssues(
                snapshot.Fields,
                snapshot.FieldMetrics,
                options.NPlusOneListPatternThreshold);

            result["nPlusOne"] = new Dictionary<string, object?>
            {
                ["issueCount"] = nPlusOneIssues.Length,
                ["issues"] = nPlusOneIssues
            };
        }

        return result;
    }

    public ExecutionProfilerRequestSample CreateRequestSample(
        string operationType,
        string? operationName)
        => CreateRequestSample(operationType, operationName, DateTimeOffset.UtcNow);

    public ExecutionProfilerRequestSample CreateRequestSample(
        string operationType,
        string? operationName,
        DateTimeOffset capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(operationType))
        {
            operationType = "unknown";
        }

        var snapshot = CaptureRequestSampleSnapshot();
        var fields = new ExecutionProfilerFieldSample[snapshot.Fields.Length];

        for (var i = 0; i < snapshot.Fields.Length; i++)
        {
            var field = snapshot.Fields[i];
            fields[i] = new ExecutionProfilerFieldSample(
                field.Coordinate,
                field.ObjectType,
                field.FieldName,
                field.DurationNanoseconds);
        }

        return new ExecutionProfilerRequestSample(
            capturedAtUtc,
            operationType,
            operationName,
            snapshot.RequestDurationNanoseconds,
            fields);
    }

    private ExecutionProfileFieldMetrics GetOrCreateFieldMetrics(string path)
    {
        if (!_fieldMetrics.TryGetValue(path, out var metrics))
        {
            metrics = new ExecutionProfileFieldMetrics();
            _fieldMetrics[path] = metrics;
        }

        return metrics;
    }

    private static int GetFieldDepth(Path path)
    {
        var depth = 0;
        var current = path;

        while (!current.IsRoot)
        {
            if (current is NamePathSegment)
            {
                depth++;
            }

            current = current.Parent;
        }

        return depth;
    }

    private static long GetElapsedNanoseconds(long startTimestamp)
        => Stopwatch.GetElapsedTime(startTimestamp).Ticks * 100;

    private CollectorSnapshot CaptureSnapshot()
    {
        ExecutionProfileFieldEntry[] fields;
        Dictionary<string, ExecutionProfileFieldMetricsSnapshot> fieldMetrics;
        int dataLoaderBatchCalls;
        int dataLoaderCacheHits;
        int dataLoaderCacheMisses;

        lock (_sync)
        {
            fields = [.. _fields];
            fieldMetrics = new Dictionary<string, ExecutionProfileFieldMetricsSnapshot>(
                _fieldMetrics.Count,
                StringComparer.Ordinal);

            foreach (var fieldMetric in _fieldMetrics)
            {
                fieldMetrics[fieldMetric.Key] =
                    new ExecutionProfileFieldMetricsSnapshot(
                        fieldMetric.Value.DataLoaderBatchCalls,
                        fieldMetric.Value.DataLoaderCacheHits,
                        fieldMetric.Value.DataLoaderCacheMisses);
            }

            dataLoaderBatchCalls = _dataLoaderBatchCalls;
            dataLoaderCacheHits = _dataLoaderCacheHits;
            dataLoaderCacheMisses = _dataLoaderCacheMisses;
        }

        var requestDuration = Volatile.Read(ref _requestDurationNanoseconds);
        if (requestDuration == 0)
        {
            requestDuration = GetElapsedNanoseconds(_requestStartTimestamp);
        }

        return new CollectorSnapshot(
            fields,
            fieldMetrics,
            requestDuration,
            dataLoaderBatchCalls,
            dataLoaderCacheHits,
            dataLoaderCacheMisses);
    }

    private RequestSampleSnapshot CaptureRequestSampleSnapshot()
    {
        ExecutionProfileFieldEntry[] fields;

        lock (_sync)
        {
            fields = [.. _fields];
        }

        var requestDuration = Volatile.Read(ref _requestDurationNanoseconds);
        if (requestDuration == 0)
        {
            requestDuration = GetElapsedNanoseconds(_requestStartTimestamp);
        }

        return new RequestSampleSnapshot(fields, requestDuration);
    }

    private static object?[] AnalyzeNPlusOneIssues(
        IReadOnlyList<ExecutionProfileFieldEntry> fields,
        IReadOnlyDictionary<string, ExecutionProfileFieldMetricsSnapshot> fieldMetrics,
        int threshold)
    {
        if (threshold < 2)
        {
            threshold = 2;
        }

        var patternMetrics = new Dictionary<string, NPlusOnePatternMetrics>(StringComparer.Ordinal);

        for (var i = 0; i < fields.Count; i++)
        {
            var path = fields[i].Path;
            var normalizedPath = NormalizeIndexedPath(path);

            if (normalizedPath.Equals(path, StringComparison.Ordinal))
            {
                continue;
            }

            if (!patternMetrics.TryGetValue(normalizedPath, out var patternMetric))
            {
                patternMetric = new NPlusOnePatternMetrics(path);
                patternMetrics[normalizedPath] = patternMetric;
            }

            patternMetric.Occurrences++;

            if (fieldMetrics.TryGetValue(path, out var metric))
            {
                patternMetric.DataLoaderBatchCalls += metric.DataLoaderBatchCalls;
                patternMetric.DataLoaderCacheHits += metric.DataLoaderCacheHits;
            }
        }

        var issues = new List<object?>(patternMetrics.Count);

        foreach (var entry in patternMetrics)
        {
            var patternMetric = entry.Value;

            if (patternMetric.Occurrences < threshold)
            {
                continue;
            }

            if (patternMetric.DataLoaderBatchCalls > 0
                || patternMetric.DataLoaderCacheHits > 0)
            {
                continue;
            }

            issues.Add(new Dictionary<string, object?>
            {
                ["pathPattern"] = entry.Key,
                ["occurrences"] = patternMetric.Occurrences,
                ["examplePath"] = patternMetric.ExamplePath,
                ["message"] =
                    "Potential N+1 pattern detected for repeated list field resolution without DataLoader batching.",
                ["recommendation"] = "Use DataLoader to batch this field access by parent keys."
            });
        }

        return [.. issues];
    }

    private static string NormalizeIndexedPath(string path)
    {
        if (path.Length == 0)
        {
            return path;
        }

        var normalized = new StringBuilder(path.Length);

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

    private sealed record ExecutionProfileFieldEntry(
        string Path,
        int Depth,
        long DurationNanoseconds,
        string Coordinate,
        string ObjectType,
        string FieldName);

    private sealed class ExecutionProfileFieldMetrics
    {
        public int DataLoaderBatchCalls { get; set; }

        public int DataLoaderCacheHits { get; set; }

        public int DataLoaderCacheMisses { get; set; }
    }

    private sealed record ExecutionProfileFieldMetricsSnapshot(
        int DataLoaderBatchCalls,
        int DataLoaderCacheHits,
        int DataLoaderCacheMisses);

    private sealed class NPlusOnePatternMetrics(string examplePath)
    {
        public string ExamplePath { get; } = examplePath;

        public int Occurrences { get; set; }

        public int DataLoaderBatchCalls { get; set; }

        public int DataLoaderCacheHits { get; set; }
    }

    private sealed record CollectorSnapshot(
        ExecutionProfileFieldEntry[] Fields,
        Dictionary<string, ExecutionProfileFieldMetricsSnapshot> FieldMetrics,
        long RequestDurationNanoseconds,
        int DataLoaderBatchCalls,
        int DataLoaderCacheHits,
        int DataLoaderCacheMisses);

    private sealed record RequestSampleSnapshot(
        ExecutionProfileFieldEntry[] Fields,
        long RequestDurationNanoseconds);
}
