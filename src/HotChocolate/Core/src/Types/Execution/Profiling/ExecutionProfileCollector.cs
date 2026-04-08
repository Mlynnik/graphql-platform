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
    private readonly long _requestStartTimestamp = Stopwatch.GetTimestamp();
    private long _requestDurationNanoseconds;

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
            serializedFields[i] = new Dictionary<string, object?>
            {
                ["path"] = fields[i].Path,
                ["depth"] = fields[i].Depth,
                ["durationNs"] = fields[i].DurationNanoseconds
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
            ["fields"] = serializedFields
        };
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
}
