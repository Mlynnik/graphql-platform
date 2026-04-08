using System.Diagnostics;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Resolvers;

namespace HotChocolate.Execution.Profiling;

internal sealed class ExecutionProfilerDiagnosticEventListener
    : ExecutionDiagnosticEventListener
{
    public override bool EnableResolveFieldValue => true;

    public override IDisposable ResolveFieldValue(IMiddlewareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Features.TryGet<ExecutionProfileCollector>(out var collector))
        {
            return EmptyScope;
        }

        return new ResolveFieldScope(
            collector,
            context.Path,
            context.Selection.Field.Coordinate.ToString(),
            context.Selection.DeclaringType.Name,
            context.Selection.Field.Name);
    }

    private sealed class ResolveFieldScope : IDisposable
    {
        private readonly ExecutionProfileCollector _collector;
        private readonly Path _path;
        private readonly string _coordinate;
        private readonly string _objectType;
        private readonly string _fieldName;
        private readonly IDisposable _pathScope;
        private readonly long _startTimestamp = Stopwatch.GetTimestamp();

        public ResolveFieldScope(
            ExecutionProfileCollector collector,
            Path path,
            string coordinate,
            string objectType,
            string fieldName)
        {
            _collector = collector;
            _path = path;
            _coordinate = coordinate;
            _objectType = objectType;
            _fieldName = fieldName;
            _pathScope = ExecutionProfilerScopeContext.Enter(path);
        }

        public void Dispose()
        {
            try
            {
                _collector.AddField(
                    _path,
                    Stopwatch.GetElapsedTime(_startTimestamp).Ticks * 100,
                    _coordinate,
                    _objectType,
                    _fieldName);
            }
            finally
            {
                _pathScope.Dispose();
            }
        }
    }
}
