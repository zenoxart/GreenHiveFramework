using System.Text.Json;

namespace GreenHive.NET.Contracts.Api;

// ---------------- Identity (/api/identity) ----------------

public sealed record LoginRequest(string UserName, string Password);

public sealed record UserInfo(Guid Id, string UserName, string DisplayName, string Email, Guid TenantId)
{
    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<string> Permissions { get; init; } = [];

    public decimal ApprovalLimit { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTimeOffset? LastLoginAt { get; init; }
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserInfo User);

/// <summary>Profile picture of a user (PNG or JPEG, base64, at most 512 KB).</summary>
public sealed record AvatarInfo(string ContentType, string DataBase64)
{
    public DateTimeOffset UpdatedAt { get; init; }

    public const int MaxBytes = 512 * 1024;
}

/// <summary>The signed-in user changes the own password.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword)
{
    public const int MinLength = 8;
}

/// <summary>Internal: token exchange for the workflow engine and the AI agent (acting for the RunAs user).</summary>
public sealed record DelegateTokenRequest(Guid UserId, string Actor);

public sealed record PermissionInfo(string Name, string ModuleId, string Description);

public sealed record RoleInfo(Guid Id, string Name, string Description)
{
    public IReadOnlyList<string> Permissions { get; init; } = [];

    public int UserCount { get; init; }
}

public sealed record RegisterPermissionsRequest(string ModuleId, IReadOnlyList<PermissionInfo> Permissions);

// ---------------- Configuration (/api/config) ----------------

public sealed record ConfigSetting(string Scope, string Key, JsonElement Value)
{
    public DateTimeOffset UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }
}

public sealed record ConfigSettingUpdate(JsonElement Value);

/// <summary>Well-known setting keys.</summary>
public static class SettingKeys
{
    public const string AiEnabled = "ai.enabled";
    public const string AiModelProfile = "ai.modelProfile";
    public const string PilotUsers = "updates.pilotUsers";
    public const string AutoMigrateWorkflows = "workflows.autoMigrate";
    public const string Language = "shell.language";
    public const string Theme = "shell.theme";
}

// ---------------- Updates (/api/updates) ----------------

public enum RolloutStage
{
    Pilot,
    Broad,
    Complete,
    Held,
    Cancelled,
}

public sealed record RolloutInfo(Guid Id, string ModuleId, string? FromVersion, string ToVersion, RolloutStage Stage)
{
    public string? HoldReason { get; init; }

    public Guid? ImpactReportId { get; init; }

    public int BrokenWorkflows { get; init; }

    public IReadOnlyList<Guid> PilotUsers { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record CreateRolloutRequest(string ModuleId, string? FromVersion, string ToVersion)
{
    public Guid? ImpactReportId { get; init; }

    public int BrokenWorkflows { get; init; }

    public int ReviewWorkflows { get; init; }
}

/// <summary>A module version the current user should run (pilot or general availability).</summary>
public sealed record ModuleAssignment(string ModuleId, string Version, RolloutStage Stage);

// ---------------- Shared helpers ----------------

/// <summary>Value/label pair for entity reference fields (EntityRef lookups) and pick lists.</summary>
public sealed record LookupItem(string Value, string Label);
