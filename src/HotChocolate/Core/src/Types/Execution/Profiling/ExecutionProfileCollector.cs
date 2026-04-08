using System.Diagnostics;
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

    public void AddField(Path path, long durationNanoseconds)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (durationNanoseconds < 0)
        {
            durationNanoseconds = 0;
        }

        var field = new ExecutionProfileFieldEntry(
            path.Print(),
            GetFieldDepth(path),
            durationNanoseconds);

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

    public IReadOnlyDictionary<string, object?> CreateResultExtension()
    {
        ExecutionProfileFieldEntry[] fields;

        lock (_sync)
        {
            fields = [.. _fields];
        }

        var serializedFields = new object?[fields.Length];

        for (var i = 0; i < fields.Length; i++)
        {
            _fieldMetrics.TryGetValue(fields[i].Path, out var fieldMetrics);
            serializedFields[i] = new Dictionary<string, object?>
            {
                ["path"] = fields[i].Path,
                ["depth"] = fields[i].Depth,
                ["durationNs"] = fields[i].DurationNanoseconds,
                ["dataLoaderBatchCalls"] = fieldMetrics?.DataLoaderBatchCalls ?? 0,
                ["dataLoaderCacheHits"] = fieldMetrics?.DataLoaderCacheHits ?? 0,
                ["dataLoaderCacheMisses"] = fieldMetrics?.DataLoaderCacheMisses ?? 0
            };
        }

        var requestDuration = Volatile.Read(ref _requestDurationNanoseconds);
        if (requestDuration == 0)
        {
            requestDuration = GetElapsedNanoseconds(_requestStartTimestamp);
        }

        return new Dictionary<string, object?>
        {
            ["requestDurationNs"] = requestDuration,
            ["fieldCount"] = fields.Length,
            ["dataLoaderBatchCalls"] = _dataLoaderBatchCalls,
            ["dataLoaderCacheHits"] = _dataLoaderCacheHits,
            ["dataLoaderCacheMisses"] = _dataLoaderCacheMisses,
            ["fields"] = serializedFields
        };
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

    private sealed record ExecutionProfileFieldEntry(
        string Path,
        int Depth,
        long DurationNanoseconds);

    private sealed class ExecutionProfileFieldMetrics
    {
        public int DataLoaderBatchCalls { get; set; }

        public int DataLoaderCacheHits { get; set; }

        public int DataLoaderCacheMisses { get; set; }
    }
}
