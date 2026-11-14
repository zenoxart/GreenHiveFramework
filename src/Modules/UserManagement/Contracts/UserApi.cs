// Shared by GreenHive.NET.Modules.UserManagement.Backend and .Client (compiled into both - one wire format).
namespace GreenHive.NET.Modules.UserManagement.Contracts;

public static class UserPermissions
{
    public const string View = "users.view";
    public const string Edit = "users.edit";
    public const string Roles = "roles.edit";
}

public sealed record UserDto(Guid Id, string UserName, string DisplayName, string Email, bool IsActive)
{
    public Guid? DepartmentId { get; init; }

    public string? DepartmentName { get; init; }

    public string JobTitle { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public Guid? ManagerId { get; init; }

    public string? ManagerName { get; init; }

    public DateOnly? HiredOn { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public decimal ApprovalLimit { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public int RowVersion { get; init; } = 1;
}

public sealed record CreateUserRequest(string UserName, string DisplayName, string Email)
{
    public Guid? DepartmentId { get; init; }

    public string? JobTitle { get; init; }

    public string? Phone { get; init; }

    public string? Location { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = ["Viewer"];

    public decimal ApprovalLimit { get; init; }

    /// <summary>Optional initial password; a temporary password is generated when empty.</summary>
    public string? Password { get; init; }
}

public sealed record CreateUserResponse(UserDto User, string? TemporaryPassword);

public sealed record UpdateUserRequest(string DisplayName, string Email, bool IsActive, int RowVersion)
{
    public Guid? DepartmentId { get; init; }

    public string? JobTitle { get; init; }

    public string? Phone { get; init; }

    public string? Location { get; init; }

    public Guid? ManagerId { get; init; }

    public decimal ApprovalLimit { get; init; }
}

public sealed record SetRolesRequest(IReadOnlyList<string> Roles);

public sealed record DepartmentDto(Guid Id, string Name, string CostCenter, int UserCount);

public sealed record RoleSummary(string Name, string Description, int UserCount);

public sealed record ResetPasswordResponse(string TemporaryPassword);
