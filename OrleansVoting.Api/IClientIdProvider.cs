namespace OrleansVoting.Api;

/// <summary>
/// Provides client identification for request tracking and throttling.
/// </summary>
public interface IClientIdProvider
{
    /// <summary>
    /// Gets a unique identifier for the current request's client.
    /// </summary>
    string GetClientId(HttpContext context);
}

/// <summary>
/// Default implementation that uses the client's IP address as identifier.
/// </summary>
public class IpAddressClientIdProvider : IClientIdProvider
{
    public string GetClientId(HttpContext context)
    {
        // Use IP address as client identifier
        // In production, you might use authenticated user ID or session cookies
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
