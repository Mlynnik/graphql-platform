using System.Globalization;

namespace HotChocolate.Execution.Profiling;

internal sealed class ExecutionProfilerSlidingWindowAggregator : IExecutionProfilerAggregationStore
{
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private readonly Queue<ExecutionProfilerRequestSample> _samples = [];
    private readonly ExecutionProfilerOptions _options;

    public ExecutionProfilerSlidingWindowAggregator(ExecutionProfilerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Add(ExecutionProfilerRequestSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!_options.AggregationEnabled)
        {
            return;
        }

        lock (_sync)
        {
            _samples.Enqueue(sample);
            TrimWindow(sample.CapturedAtUtc);
        }
    }

    public IReadOnlyDictionary<string, object?> CreateSnapshot()
    {
        if (!_options.AggregationEnabled)
        {
            return new Dictionary<string, object?>(0);
        }

        ExecutionProfilerRequestSample[] samples;

        lock (_sync)
        {
            TrimWindow(DateTimeOffset.UtcNow);
            samples = [.. _samples];
        }

        return CreateSnapshot(samples);
    }

    private IReadOnlyDictionary<string, object?> CreateSnapshot(
        IReadOnlyList<ExecutionProfilerRequestSample> samples)
    {
        var requestDuration = new DurationAccumulator();
        var byOperationType = new Dictionary<string, DurationAccumulator>(StringComparer.Ordinal);
        var byOperation = new Dictionary<OperationGroupKey, DurationAccumulator>();
        var byObjectType = new Dictionary<string, DurationAccumulator>(StringComparer.Ordinal);
        var byField = new Dictionary<string, FieldGroupAccumulator>(StringComparer.Ordinal);

        for (var requestIndex = 0; requestIndex < samples.Count; requestIndex++)
        {
            var requestSample = samples[requestIndex];
            requestDuration.Add(requestSample.RequestDurationNanoseconds);

            var operationTypeAccumulator = GetOrCreate(byOperationType, requestSample.OperationType);
            operationTypeAccumulator.Add(requestSample.RequestDurationNanoseconds);

            var operationAccumulator = GetOrCreate(
                byOperation,
                new OperationGroupKey(requestSample.OperationType, requestSample.OperationName));
            operationAccumulator.Add(requestSample.RequestDurationNanoseconds);

            for (var fieldIndex = 0; fieldIndex < requestSample.Fields.Count; fieldIndex++)
            {
                var fieldSample = requestSample.Fields[fieldIndex];
                GetOrCreate(byObjectType, fieldSample.ObjectType).Add(fieldSample.DurationNanoseconds);
                GetOrCreate(byField, fieldSample.Coordinate, fieldSample.ObjectType, fieldSample.FieldName)
                    .Duration.Add(fieldSample.DurationNanoseconds);
            }
        }

        var fromUtc = samples.Count > 0
            ? samples[0].CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture)
            : null;
        var toUtc = samples.Count > 0
            ? samples[samples.Count - 1].CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture)
            : null;

        return new Dictionary<string, object?>
        {
            ["window"] = new Dictionary<string, object?>
            {
                ["requestCount"] = samples.Count,
                ["maxRequests"] = _options.SlidingWindowMaxRequests,
                ["durationSeconds"] = (long)_options.SlidingWindowDuration.TotalSeconds,
                ["fromUtc"] = fromUtc,
                ["toUtc"] = toUtc
            },
            ["requestDurationNs"] = requestDuration.CreateSnapshot(),
            ["byOperationType"] = SerializeByOperationType(byOperationType),
            ["byOperation"] = SerializeByOperation(byOperation),
            ["byObjectType"] = SerializeByObjectType(byObjectType),
            ["byField"] = SerializeByField(byField)
        };
    }

    private void TrimWindow(DateTimeOffset nowUtc)
    {
        if (_options.SlidingWindowDuration > TimeSpan.Zero)
        {
            var minTimestamp = nowUtc - _options.SlidingWindowDuration;

            while (_samples.Count > 0 && _samples.Peek().CapturedAtUtc < minTimestamp)
            {
                _samples.Dequeue();
            }
        }

        if (_options.SlidingWindowMaxRequests > 0)
        {
            while (_samples.Count > _options.SlidingWindowMaxRequests)
            {
                _samples.Dequeue();
            }
        }
    }

    private static DurationAccumulator GetOrCreate(
        IDictionary<string, DurationAccumulator> source,
        string key)
    {
        if (!source.TryGetValue(key, out var accumulator))
        {
            accumulator = new DurationAccumulator();
            source[key] = accumulator;
        }

        return accumulator;
    }

    private static DurationAccumulator GetOrCreate(
        IDictionary<OperationGroupKey, DurationAccumulator> source,
        OperationGroupKey key)
    {
        if (!source.TryGetValue(key, out var accumulator))
        {
            accumulator = new DurationAccumulator();
            source[key] = accumulator;
        }

        return accumulator;
    }

    private static FieldGroupAccumulator GetOrCreate(
        IDictionary<string, FieldGroupAccumulator> source,
        string key,
        string objectType,
        string fieldName)
    {
        if (!source.TryGetValue(key, out var accumulator))
        {
            accumulator = new FieldGroupAccumulator(objectType, fieldName);
            source[key] = accumulator;
        }

        return accumulator;
    }

    private static object?[] SerializeByOperationType(
        IReadOnlyDictionary<string, DurationAccumulator> byOperationType)
    {
        var entries = byOperationType
            .OrderBy(static t => t.Key, StringComparer.Ordinal)
            .ToArray();
        var serialized = new object?[entries.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            serialized[i] = new Dictionary<string, object?>
            {
                ["operationType"] = entries[i].Key,
                ["requestCount"] = entries[i].Value.Count,
                ["requestDurationNs"] = entries[i].Value.CreateSnapshot()
            };
        }

        return serialized;
    }

    private static object?[] SerializeByOperation(
        IReadOnlyDictionary<OperationGroupKey, DurationAccumulator> byOperation)
    {
        var entries = byOperation
            .OrderBy(static t => t.Key.OperationType, StringComparer.Ordinal)
            .ThenBy(static t => t.Key.OperationName, StringComparer.Ordinal)
            .ToArray();
        var serialized = new object?[entries.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            serialized[i] = new Dictionary<string, object?>
            {
                ["operationType"] = entries[i].Key.OperationType,
                ["operationName"] = entries[i].Key.OperationName,
                ["requestCount"] = entries[i].Value.Count,
                ["requestDurationNs"] = entries[i].Value.CreateSnapshot()
            };
        }

        return serialized;
    }

    private static object?[] SerializeByObjectType(
        IReadOnlyDictionary<string, DurationAccumulator> byObjectType)
    {
        var entries = byObjectType
            .OrderBy(static t => t.Key, StringComparer.Ordinal)
            .ToArray();
        var serialized = new object?[entries.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            serialized[i] = new Dictionary<string, object?>
            {
                ["objectType"] = entries[i].Key,
                ["fieldCallCount"] = entries[i].Value.Count,
                ["fieldDurationNs"] = entries[i].Value.CreateSnapshot()
            };
        }

        return serialized;
    }

    private static object?[] SerializeByField(
        IReadOnlyDictionary<string, FieldGroupAccumulator> byField)
    {
        var entries = byField
            .OrderBy(static t => t.Key, StringComparer.Ordinal)
            .ToArray();
        var serialized = new object?[entries.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            serialized[i] = new Dictionary<string, object?>
            {
                ["coordinate"] = entries[i].Key,
                ["objectType"] = entries[i].Value.ObjectType,
                ["fieldName"] = entries[i].Value.FieldName,
                ["callCount"] = entries[i].Value.Duration.Count,
                ["durationNs"] = entries[i].Value.Duration.CreateSnapshot()
            };
        }

        return serialized;
    }

    private sealed class DurationAccumulator
    {
        private readonly List<long> _samples = [];
        private long _min = long.MaxValue;
        private long _max = long.MinValue;
        private double _sum;

        public int Count => _samples.Count;

        public void Add(long durationNanoseconds)
        {
            if (durationNanoseconds < 0)
            {
                durationNanoseconds = 0;
            }

            _samples.Add(durationNanoseconds);
            _sum += durationNanoseconds;

            if (durationNanoseconds < _min)
            {
                _min = durationNanoseconds;
            }

            if (durationNanoseconds > _max)
            {
                _max = durationNanoseconds;
            }
        }

        public IReadOnlyDictionary<string, object?> CreateSnapshot()
        {
            if (_samples.Count == 0)
            {
                return new Dictionary<string, object?>
                {
                    ["minNs"] = 0L,
                    ["maxNs"] = 0L,
                    ["avgNs"] = 0.0d,
                    ["p50Ns"] = 0L,
                    ["p95Ns"] = 0L,
                    ["p99Ns"] = 0L
                };
            }

            var ordered = new List<long>(_samples);
            ordered.Sort();

            return new Dictionary<string, object?>
            {
                ["minNs"] = _min,
                ["maxNs"] = _max,
                ["avgNs"] = _sum / _samples.Count,
                ["p50Ns"] = GetPercentile(ordered, 0.50d),
                ["p95Ns"] = GetPercentile(ordered, 0.95d),
                ["p99Ns"] = GetPercentile(ordered, 0.99d)
            };
        }

        private static long GetPercentile(
            IReadOnlyList<long> orderedValues,
            double percentile)
        {
            if (orderedValues.Count == 0)
            {
                return 0;
            }

            var rank = (int)Math.Ceiling(orderedValues.Count * percentile) - 1;
            if (rank < 0)
            {
                rank = 0;
            }
            else if (rank >= orderedValues.Count)
            {
                rank = orderedValues.Count - 1;
            }

            return orderedValues[rank];
        }
    }

    private sealed class FieldGroupAccumulator(string objectType, string fieldName)
    {
        public string ObjectType { get; } = objectType;

        public string FieldName { get; } = fieldName;

        public DurationAccumulator Duration { get; } = new();
    }

    private readonly record struct OperationGroupKey(
        string OperationType,
        string? OperationName);
}
