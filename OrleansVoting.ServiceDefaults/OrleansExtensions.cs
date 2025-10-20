using Orleans;
using Orleans.Hosting;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring Orleans in host applications.
/// </summary>
public static class OrleansExtensions
{
    /// <summary>
    /// Configures the host as an Orleans client (no grain hosting).
    /// Use this for web frontends and services that only call grains.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder for chaining.</returns>
    public static IHostApplicationBuilder UseOrleansClient(
        this IHostApplicationBuilder builder)
    {
        // Configure Orleans client
        builder.UseOrleansClient(clientBuilder =>
        {
            // Client-specific configuration
            // Clustering configuration will be provided by Aspire via .WithReference(orleans)
            // No additional configuration needed here for basic client setup
        });

        return builder;
    }
}
