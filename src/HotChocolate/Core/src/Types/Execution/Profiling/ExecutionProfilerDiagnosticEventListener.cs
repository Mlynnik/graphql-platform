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

        return new ResolveFieldScope(collector, context.Path);
    }

    private sealed class ResolveFieldScope : IDisposable
    {
        private readonly ExecutionProfileCollector _collector;
        private readonly Path _path;
        private readonly IDisposable _pathScope;
        private readonly long _startTimestamp = Stopwatch.GetTimestamp();

        public ResolveFieldScope(ExecutionProfileCollector collector, Path path)
        {
            _collector = collector;
            _path = path;
            _pathScope = ExecutionProfilerScopeContext.Enter(path);
        }

        public void Dispose()
        {
            try
            {
                _collector.AddField(
                    _path,
                    Stopwatch.GetElapsedTime(_startTimestamp).Ticks * 100);
            }
            finally
            {
                _pathScope.Dispose();
            }
        }
    }
}
