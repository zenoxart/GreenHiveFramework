using System.Text.Json.Nodes;

namespace GreenHive.NET.NotificationContracts;

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

public enum AudienceKind
{
    /// <summary>Everyone in the tenant.</summary>
    Tenant,

    /// <summary>One user.</summary>
    User,

    /// <summary>Every user who has the permission.</summary>
    Permission,

    /// <summary>Only backend subscribers (workflow triggers, impact analysis) - not pushed to clients.</summary>
    System,
}

/// <summary>Who receives a notification.</summary>
public sealed record NotificationAudience(AudienceKind Kind, string? Value = null)
{
    public static NotificationAudience Everyone { get; } = new(AudienceKind.Tenant);

    public static NotificationAudience Backend { get; } = new(AudienceKind.System);

    public static NotificationAudience ForUser(Guid userId) => new(AudienceKind.User, userId.ToString());

    public static NotificationAudience WithPermission(string permission) => new(AudienceKind.Permission, permission);
}

/// <summary>Envelope of every notification / domain event.</summary>
public sealed record NotificationEnvelope
{
    /// <summary>Event ID (de-duplication).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Sequence number assigned by the NotificationPoint (catch-up after reconnect).</summary>
    public long Sequence { get; init; }

    public required string Type { get; init; }

    /// <summary>Publishing module or service.</summary>
    public required string Source { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public Guid TenantId { get; init; }

    public NotificationAudience Audience { get; init; } = NotificationAudience.Everyone;

    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;

    /// <summary>TextKey or plain title for the in-app / toast notification (null = no visible notification).</summary>
    public string? Title { get; init; }

    public string? Message { get; init; }

    public JsonObject? Payload { get; init; }

    /// <summary>Optional navigation target (key) and parameter used when the notification is activated.</summary>
    public string? NavigationTarget { get; init; }

    public string? NavigationParameter { get; init; }
}
