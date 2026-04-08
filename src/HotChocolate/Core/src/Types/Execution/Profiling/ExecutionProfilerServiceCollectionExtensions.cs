using HotChocolate.Execution.Profiling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for execution profiler service registration.
/// </summary>
public static class ExecutionProfilerServiceCollectionExtensions
{
    /// <summary>
    /// Adds execution profiler services.
    /// </summary>
    /// <param name="services">
    /// The service collection.
    /// </param>
    /// <returns>
    /// Returns the service collection.
    /// </returns>
    public static IServiceCollection AddExecutionProfiler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(
            static serviceProvider =>
            {
                var options = new ExecutionProfilerOptions();

                foreach (var configure in serviceProvider.GetServices<Action<ExecutionProfilerOptions>>())
                {
                    configure(options);
                }

                return options;
            });

        services.TryAddSingleton<IExecutionProfilerState>(
            static serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<ExecutionProfilerOptions>();
                return new ExecutionProfilerState(options.Enabled);
            });

        return services;
    }

    /// <summary>
    /// Adds a profiler options configuration delegate.
    /// </summary>
    /// <param name="services">
    /// The service collection.
    /// </param>
    /// <param name="configure">
    /// The delegate used to configure profiler options.
    /// </param>
    /// <returns>
    /// Returns the service collection.
    /// </returns>
    public static IServiceCollection ConfigureExecutionProfiler(
        this IServiceCollection services,
        Action<ExecutionProfilerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddExecutionProfiler();
        services.AddSingleton(configure);
        return services;
    }

    /// <summary>
    /// Adds profiler options from configuration.
    /// </summary>
    /// <param name="services">
    /// The service collection.
    /// </param>
    /// <param name="configuration">
    /// The application configuration.
    /// </param>
    /// <param name="sectionPath">
    /// The profiler section path.
    /// </param>
    /// <returns>
    /// Returns the service collection.
    /// </returns>
    public static IServiceCollection ConfigureExecutionProfiler(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionPath = ExecutionProfilerOptions.DefaultConfigurationSectionPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.ConfigureExecutionProfiler(
            options =>
            {
                var section = GetConfigurationSection(configuration, sectionPath);

                if (bool.TryParse(section["Enabled"], out var enabled))
                {
                    options.Enabled = enabled;
                }

                var detailLevel = section["DetailLevel"];
                if (Enum.TryParse<ExecutionProfilerDetailLevel>(
                    detailLevel,
                    ignoreCase: true,
                    out var parsedDetailLevel))
                {
                    options.DetailLevel = parsedDetailLevel;
                }

                if (int.TryParse(section["NPlusOneListPatternThreshold"], out var threshold)
                    && threshold > 0)
                {
                    options.NPlusOneListPatternThreshold = threshold;
                }
            });
    }

    private static IConfiguration GetConfigurationSection(
        IConfiguration configuration,
        string sectionPath)
    {
        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            return configuration;
        }

        return configuration.GetSection(sectionPath);
    }
}
