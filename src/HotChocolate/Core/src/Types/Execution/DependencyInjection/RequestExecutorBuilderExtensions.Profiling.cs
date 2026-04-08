using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution.Pipeline;
using HotChocolate.Execution.Profiling;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static partial class RequestExecutorBuilderExtensions
{
    /// <summary>
    /// Adds execution profiler services and middleware.
    /// </summary>
    /// <param name="builder">
    /// The request executor builder.
    /// </param>
    /// <returns>
    /// Returns the request executor builder.
    /// </returns>
    public static IRequestExecutorBuilder AddExecutionProfiler(
        this IRequestExecutorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddExecutionProfiler();

        return builder
            .AddApplicationService<ExecutionProfilerOptions>()
            .AddApplicationService<IExecutionProfilerState>()
            .AddApplicationService<IExecutionProfilerAggregationStore>()
            .AddApplicationService<IExecutionProfilerMetricsExporter>()
            .AddDiagnosticEventListener<ExecutionProfilerDiagnosticEventListener>()
            .AddDiagnosticEventListener<ExecutionProfilerDataLoaderDiagnosticEventListener>()
            .UseRequest(
                ExecutionProfilerMiddleware.Create(),
                after: WellKnownRequestMiddleware.InstrumentationMiddleware,
                allowMultiple: false);
    }

    /// <summary>
    /// Adds execution profiler services and middleware and configures options.
    /// </summary>
    /// <param name="builder">
    /// The request executor builder.
    /// </param>
    /// <param name="configure">
    /// The options configuration action.
    /// </param>
    /// <returns>
    /// Returns the request executor builder.
    /// </returns>
    public static IRequestExecutorBuilder AddExecutionProfiler(
        this IRequestExecutorBuilder builder,
        Action<ExecutionProfilerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.ConfigureExecutionProfiler(configure);
        return builder.AddExecutionProfiler();
    }

    /// <summary>
    /// Adds execution profiler services and middleware and configures options from configuration.
    /// </summary>
    /// <param name="builder">
    /// The request executor builder.
    /// </param>
    /// <param name="configuration">
    /// The application configuration.
    /// </param>
    /// <param name="sectionPath">
    /// The profiler section path.
    /// </param>
    /// <returns>
    /// Returns the request executor builder.
    /// </returns>
    public static IRequestExecutorBuilder AddExecutionProfiler(
        this IRequestExecutorBuilder builder,
        IConfiguration configuration,
        string sectionPath = ExecutionProfilerOptions.DefaultConfigurationSectionPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.ConfigureExecutionProfiler(configuration, sectionPath);
        return builder.AddExecutionProfiler();
    }

    /// <summary>
    /// Modifies execution profiler options.
    /// </summary>
    /// <param name="builder">
    /// The request executor builder.
    /// </param>
    /// <param name="configure">
    /// The options configuration action.
    /// </param>
    /// <returns>
    /// Returns the request executor builder.
    /// </returns>
    public static IRequestExecutorBuilder ModifyExecutionProfilerOptions(
        this IRequestExecutorBuilder builder,
        Action<ExecutionProfilerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.ConfigureExecutionProfiler(configure);
        return builder;
    }
}
