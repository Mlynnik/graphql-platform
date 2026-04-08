using GreenDonut;

namespace HotChocolate.Execution.Profiling;

internal sealed class ExecutionProfilerDataLoaderDiagnosticEventListener
    : DataLoaderDiagnosticEventListener
{
    private readonly IRequestContextAccessor _requestContextAccessor;

    public ExecutionProfilerDataLoaderDiagnosticEventListener(
        IRequestContextAccessor requestContextAccessor)
    {
        _requestContextAccessor = requestContextAccessor
            ?? throw new ArgumentNullException(nameof(requestContextAccessor));
    }

    public override void ResolvedTaskFromCache(
        IDataLoader dataLoader,
        PromiseCacheKey cacheKey,
        Task task)
    {
        if (TryGetCollector(out var collector))
        {
            collector.AddDataLoaderCacheHit(ExecutionProfilerScopeContext.CurrentPath);
        }
    }

    public override IDisposable ExecuteBatch<TKey>(
        IDataLoader dataLoader,
        IReadOnlyList<TKey> keys)
    {
        if (TryGetCollector(out var collector))
        {
            collector.AddDataLoaderBatch(
                ExecutionProfilerScopeContext.CurrentPath,
                keys.Count);
        }

        return EmptyScope;
    }

    private bool TryGetCollector(out ExecutionProfileCollector collector)
    {
        try
        {
            var requestContext = _requestContextAccessor.RequestContext;
            if (requestContext.Features.TryGet<ExecutionProfileCollector>(out var profileCollector)
                && profileCollector is not null)
            {
                collector = profileCollector;
                return true;
            }
        }
        catch (InvalidCastException)
        {
        }

        collector = null!;
        return false;
    }
}
