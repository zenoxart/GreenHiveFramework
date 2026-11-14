namespace GreenHive.NET.Backend.Core.Hosting;

/// <summary>Configuration section "GreenHive" (environment variables GreenHive__*).</summary>
public sealed class GreenHiveOptions
{
    public const string Section = "GreenHive";

    /// <summary>Name of this service (source of events, audit entries and logs).</summary>
    public string ServiceName { get; set; } = "service";

    /// <summary>Shared signing key of the JWTs (at least 32 characters).</summary>
    public string JwtKey { get; set; } = "development-only-signing-key-change-me-0123456789";

    public string JwtIssuer { get; set; } = "greenhive";

    public string JwtAudience { get; set; } = "greenhive";

    public int TokenLifetimeMinutes { get; set; } = 480;

    /// <summary>Key for service-to-service calls of /internal endpoints (never routed by the gateway).</summary>
    public string InternalKey { get; set; } = "development-internal-key";

    /// <summary>Base URLs of other services by name (notification-point, identity-service, api-gateway, ...).</summary>
    public Dictionary<string, string> Services { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Include exception details in error responses (development only).</summary>
    public bool ShowErrorDetails { get; set; }

    /// <summary>Dispatch the outbox of this service to the NotificationPoint.</summary>
    public bool EnableOutbox { get; set; } = true;

    public string? ServiceUrl(string name) => Services.TryGetValue(name, out var url) ? url.TrimEnd('/') : null;
}
