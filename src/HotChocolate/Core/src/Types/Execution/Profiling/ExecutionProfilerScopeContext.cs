namespace HotChocolate.Execution.Profiling;

internal static class ExecutionProfilerScopeContext
{
    private static readonly AsyncLocal<Path?> s_currentPath = new();

    public static Path? CurrentPath => s_currentPath.Value;

    public static IDisposable Enter(Path path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var previousPath = s_currentPath.Value;
        s_currentPath.Value = path;
        return new Scope(previousPath);
    }

    private sealed class Scope(Path? previousPath) : IDisposable
    {
        public void Dispose()
        {
            s_currentPath.Value = previousPath;
        }
    }
}
