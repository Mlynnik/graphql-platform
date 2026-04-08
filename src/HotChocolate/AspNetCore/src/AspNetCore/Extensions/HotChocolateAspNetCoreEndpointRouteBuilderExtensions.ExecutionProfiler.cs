using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides endpoint mapping extensions for execution profiler diagnostics.
/// </summary>
public static class HotChocolateAspNetCoreEndpointRouteBuilderExtensions
{
    private const string GraphQLProfilerPath = "/graphql/profiler";

    /// <summary>
    /// Maps an endpoint that returns current GraphQL execution profiler statistics.
    /// The endpoint returns <c>404</c> outside development environments.
    /// </summary>
    /// <param name="endpointRouteBuilder">
    /// The endpoint route builder.
    /// </param>
    /// <param name="pattern">
    /// The route pattern.
    /// </param>
    /// <param name="schemaName">
    /// The schema name.
    /// </param>
    /// <returns>
    /// Returns the endpoint convention builder.
    /// </returns>
    public static IEndpointConventionBuilder MapGraphQLProfiler(
        this IEndpointRouteBuilder endpointRouteBuilder,
        [StringSyntax("Route")] string pattern = GraphQLProfilerPath,
        string? schemaName = null)
    {
        ArgumentNullException.ThrowIfNull(endpointRouteBuilder);

        schemaName = ResolveSchemaName(endpointRouteBuilder.ServiceProvider, schemaName);
        var schemaNameOrDefault = schemaName ?? ISchemaDefinition.DefaultName;

        return endpointRouteBuilder
            .MapGet(
                pattern,
                async context =>
                {
                    var environment = context.RequestServices.GetService<IHostEnvironment>();
                    if (!(environment?.IsDevelopment() ?? true))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    var executorProvider =
                        context.RequestServices.GetRequiredService<IRequestExecutorProvider>();

                    var executor = await executorProvider
                        .GetExecutorAsync(schemaNameOrDefault, context.RequestAborted)
                        .ConfigureAwait(false);

                    var statistics = executor.GetExecutionProfilerStatistics();

                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsJsonAsync(
                            statistics,
                            context.RequestAborted)
                        .ConfigureAwait(false);
                })
            .WithDisplayName("Hot Chocolate GraphQL Profiler Statistics Endpoint");
    }

    private static string? ResolveSchemaName(
        IServiceProvider services,
        string? schemaName)
    {
        if (schemaName is null
            && services.GetService<IRequestExecutorProvider>() is { } provider
            && provider.SchemaNames.Length == 1)
        {
            schemaName = provider.SchemaNames[0];
        }

        return schemaName;
    }
}
