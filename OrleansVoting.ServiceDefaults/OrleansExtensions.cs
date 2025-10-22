using Orleans;
using Orleans.Hosting;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring Orleans in host applications.
/// </summary>
public static class OrleansExtensions
{
    /// <summary>
    /// Configures the host as an Orleans client (no grain hosting) with telemetry enabled.
    /// This is a wrapper around UseOrleansClient that automatically adds ActivityPropagation for distributed tracing.
    /// Use this for web frontends and services that only call grains.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Action to configure the Orleans client builder (clustering, etc.).</param>
    /// <returns>The host application builder for chaining.</returns>
    public static IHostApplicationBuilder ConfigureOrleansClient(
        this IHostApplicationBuilder builder,
        Action<IClientBuilder> configure)
    {
        builder.UseOrleansClient(clientBuilder =>
        {
            // Apply custom configuration first (clustering, etc.)
            configure(clientBuilder);

            // Always enable distributed tracing for Orleans clients
            // This must be called after clustering configuration per Microsoft Learn guidelines
            clientBuilder.AddActivityPropagation();
        });

        return builder;
    }
}
