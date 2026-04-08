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

        services.TryAddSingleton<IExecutionProfilerAggregationStore, ExecutionProfilerSlidingWindowAggregator>();
        services.TryAddSingleton<IExecutionProfilerMetricsExporter, ExecutionProfilerOpenTelemetryExporter>();

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

                AddStringSetValues(section, "IncludedOperationTypes", options.IncludedOperationTypes);
                AddStringSetValues(section, "IncludedOperationNames", options.IncludedOperationNames);
                AddStringSetValues(section, "IncludedPathPrefixes", options.IncludedPathPrefixes);
                AddStringSetValues(section, "ExcludedObjectTypes", options.ExcludedObjectTypes);
                AddStringSetValues(section, "ExcludedFieldCoordinates", options.ExcludedFieldCoordinates);
                AddStringSetValues(section, "ExcludedFieldNames", options.ExcludedFieldNames);

                if (int.TryParse(section["NPlusOneListPatternThreshold"], out var threshold)
                    && threshold > 0)
                {
                    options.NPlusOneListPatternThreshold = threshold;
                }

                if (bool.TryParse(section["AggregationEnabled"], out var aggregationEnabled))
                {
                    options.AggregationEnabled = aggregationEnabled;
                }

                if (bool.TryParse(section["OpenTelemetryEnabled"], out var openTelemetryEnabled))
                {
                    options.OpenTelemetryEnabled = openTelemetryEnabled;
                }

                if (bool.TryParse(section["OpenTelemetryIncludeOperationName"], out var includeOperationName))
                {
                    options.OpenTelemetryIncludeOperationName = includeOperationName;
                }

                if (bool.TryParse(section["SlowRequestLoggingEnabled"], out var slowRequestLoggingEnabled))
                {
                    options.SlowRequestLoggingEnabled = slowRequestLoggingEnabled;
                }

                var slowRequestThreshold = section["SlowRequestThreshold"];
                if (TimeSpan.TryParse(slowRequestThreshold, out var parsedSlowRequestThreshold)
                    && parsedSlowRequestThreshold > TimeSpan.Zero)
                {
                    options.SlowRequestThreshold = parsedSlowRequestThreshold;
                }
                else if (int.TryParse(slowRequestThreshold, out var slowRequestThresholdMs)
                    && slowRequestThresholdMs > 0)
                {
                    options.SlowRequestThreshold = TimeSpan.FromMilliseconds(slowRequestThresholdMs);
                }

                if (int.TryParse(section["SlowRequestFieldLimit"], out var slowRequestFieldLimit)
                    && slowRequestFieldLimit > 0)
                {
                    options.SlowRequestFieldLimit = slowRequestFieldLimit;
                }

                if (int.TryParse(section["SlidingWindowMaxRequests"], out var maxRequests)
                    && maxRequests > 0)
                {
                    options.SlidingWindowMaxRequests = maxRequests;
                }

                var slidingWindowDuration = section["SlidingWindowDuration"];
                if (TimeSpan.TryParse(slidingWindowDuration, out var parsedWindowDuration)
                    && parsedWindowDuration > TimeSpan.Zero)
                {
                    options.SlidingWindowDuration = parsedWindowDuration;
                }
                else if (int.TryParse(slidingWindowDuration, out var durationSeconds)
                    && durationSeconds > 0)
                {
                    options.SlidingWindowDuration = TimeSpan.FromSeconds(durationSeconds);
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

    private static void AddStringSetValues(
        IConfiguration section,
        string key,
        ISet<string> target)
    {
        AddDelimitedValues(section[key], target);

        foreach (var child in section.GetSection(key).GetChildren())
        {
            AddDelimitedValues(child.Value, target);
        }
    }

    private static void AddDelimitedValues(
        string? values,
        ISet<string> target)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return;
        }

        var splitValues = values.Split(',', ';', StringSplitOptions.TrimEntries);
        for (var i = 0; i < splitValues.Length; i++)
        {
            if (splitValues[i].Length > 0)
            {
                target.Add(splitValues[i]);
            }
        }
    }
}
