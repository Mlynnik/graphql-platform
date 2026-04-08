using System.Threading;

namespace HotChocolate.Execution.Profiling;

/// <summary>
/// Default implementation of <see cref="IExecutionProfilerState"/>.
/// </summary>
public sealed class ExecutionProfilerState : IExecutionProfilerState
{
    private int _isEnabled;

    /// <summary>
    /// Initializes a new instance of <see cref="ExecutionProfilerState"/>.
    /// </summary>
    /// <param name="enabledByDefault">
    /// A value indicating whether profiling is enabled by default.
    /// </param>
    public ExecutionProfilerState(bool enabledByDefault = false)
    {
        _isEnabled = enabledByDefault ? 1 : 0;
    }

    /// <inheritdoc />
    public bool IsEnabled => Volatile.Read(ref _isEnabled) == 1;

    /// <inheritdoc />
    public void SetEnabled(bool enabled)
    {
        Interlocked.Exchange(ref _isEnabled, enabled ? 1 : 0);
    }
}
