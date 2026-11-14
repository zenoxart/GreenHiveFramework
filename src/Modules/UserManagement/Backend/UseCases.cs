using System.Security.Cryptography;
using System.Text.Json.Nodes;
using GreenHive.NET.Modules.UserManagement.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Modules.UserManagement.Backend;

/// <summary>Creates an account with profile and roles (also the workflow step users.createUser of onboarding workflows).</summary>
public sealed class CreateUser(UserRepository repository, Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<CreateUserResponse>> ExecuteAsync(CreateUserRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(
                Validation.RequiredMax("userName", r.UserName, 40).Bind(n => System.Text.RegularExpressions.Regex.IsMatch(n, "^[a-z][a-z0-9.\\-]{2,40}$")
                    ? Validation.Valid(n) : Error.Field("userName", "users.user.userNamePattern", "users:user.userNamePattern")),
                Validation.RequiredMax("displayName", r.DisplayName, 100),
                Validation.Email("email", r.Email),
                Validation.Range("approvalLimit", r.ApprovalLimit, 0m, 10_000_000m),
                (userName, displayName, email, limit) => (UserName: userName, DisplayName: displayName, Email: email, Limit: limit)))
            .Authorize(context.User, UserPermissions.Edit)
            .Bind(valid => request.Roles.Count > 0 && !request.Roles.Any(r => r.Equals("Administrator", StringComparison.OrdinalIgnoreCase)) || context.User.Has("*")
                ? Result.Success(valid)
                : Result.Failure<(string UserName, string DisplayName, string Email, decimal Limit)>(Error.Forbidden("users.user.adminRole", "users:user.adminRoleForbidden")))
            .BindAsync(valid => repository.ExistsAsync(valid.UserName, valid.Email, ct)
                .Bind(exists => exists ? Result.Failure<(string, string, string, decimal)>(Error.Conflict("users.user.exists", "users:user.exists")) : valid))
            .BindAsync(valid => repository.RolesByNameAsync(context.User.TenantId, request.Roles, ct)
                .Ensure(roles => roles.Count == request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    Error.Field("roles", "users.user.unknownRole", "users:user.unknownRole"))
                .Map(roles => (Valid: valid, Roles: roles)))
            .BindAsync(x =>
            {
                var temporary = string.IsNullOrWhiteSpace(request.Password) ? TemporaryPassword() : null;
                var user = new UserDto(Guid.NewGuid(), x.Valid.Item1, x.Valid.Item2, x.Valid.Item3, true)
                {
                    DepartmentId = request.DepartmentId,
                    JobTitle = request.JobTitle ?? string.Empty,
                    Phone = request.Phone ?? string.Empty,
                    Location = request.Location ?? string.Empty,
                    Roles = x.Roles.Select(r => r.Name).ToList(),
                    ApprovalLimit = x.Valid.Item4,
                };
                return db.InTransactionAsync(session =>
                    repository.InsertAsync(session, user, context.User.TenantId, PasswordHasher.Hash(request.Password is { Length: > 0 } p ? p : temporary!),
                            x.Roles.Select(r => r.Id).ToList(), ct)
                        .BindAsync(_ => outbox.AddAsync(session, outbox.Create(EventTypes.UserCreated, new JsonObject
                        {
                            ["userId"] = user.Id.ToString(),
                            ["userName"] = user.UserName,
                            ["displayName"] = user.DisplayName,
                            ["roles"] = string.Join(",", user.Roles),
                        }, NotificationAudience.WithPermission(UserPermissions.View)) with
                        {
                            Title = "users:notification.userCreated",
                            Message = user.DisplayName,
                            NavigationTarget = "users.list",
                            NavigationParameter = user.Id.ToString(),
                        }, ct))
                        .BindAsync(_ => audit.WriteAsync(session, context.User, "user.created", "user", user.Id.ToString(), ct: ct))
                        .Map(_ => new CreateUserResponse(user, temporary)), ct: ct);
            });

    public static string TemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        return "Hive-" + string.Concat(Enumerable.Range(0, 10).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])) + "!";
    }
}

/// <summary>Updates account and profile (optimistic concurrency on the profile row version).</summary>
public sealed class UpdateUser(UserRepository repository, Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<UserDto>> ExecuteAsync(Guid id, UpdateUserRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(Validation.RequiredMax("displayName", r.DisplayName, 100), Validation.Email("email", r.Email),
                Validation.Range("approvalLimit", r.ApprovalLimit, 0m, 10_000_000m)))
            .Authorize(context.User, UserPermissions.Edit)
            .Ensure(_ => request.IsActive || id != context.User.Id, Error.Conflict("users.user.deactivateSelf", "users:user.deactivateSelf"))
            .BindAsync(valid => repository.FindAsync(id, ct).Map(user => user with
            {
                DisplayName = valid.Item1,
                Email = valid.Item2,
                IsActive = request.IsActive,
                ApprovalLimit = valid.Item3,
                DepartmentId = request.DepartmentId,
                JobTitle = request.JobTitle ?? string.Empty,
                Phone = request.Phone ?? string.Empty,
                Location = request.Location ?? string.Empty,
                ManagerId = request.ManagerId == id ? null : request.ManagerId,
            }))
            .BindAsync(user => db.InTransactionAsync(session =>
                repository.UpdateAsync(session, user, request.RowVersion, ct)
                    .BindAsync(_ => outbox.AddAsync(session, outbox.Create(EventTypes.UserUpdated, new JsonObject
                    {
                        ["userId"] = user.Id.ToString(),
                        ["isActive"] = user.IsActive,
                    }, NotificationAudience.WithPermission(UserPermissions.View)), ct))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "user.updated", "user", user.Id.ToString(), ct: ct)), ct: ct))
            .BindAsync(_ => repository.FindAsync(id, ct));
}

/// <summary>Replaces the roles of a user and tells the user's clients to refresh their permissions.</summary>
public sealed class SetUserRoles(UserRepository repository, Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<UserDto>> ExecuteAsync(Guid id, SetRolesRequest request, CancellationToken ct) =>
        Steps.Authorize(context.User, UserPermissions.Roles)
            .Ensure(_ => request.Roles.Count > 0, Error.Field("roles", "users.roles.empty", "validation:required"))
            .BindAsync(_ => repository.RolesByNameAsync(context.User.TenantId, request.Roles, ct))
            .Ensure(roles => roles.Count == request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Error.Field("roles", "users.user.unknownRole", "users:user.unknownRole"))
            .Ensure(roles => context.User.Has("*") || roles.All(r => r.Name != "Administrator"), Error.Forbidden("users.user.adminRole", "users:user.adminRoleForbidden"))
            .BindAsync(roles => db.InTransactionAsync(session =>
                repository.SetRolesAsync(session, id, roles.Select(r => r.Id).ToList(), ct)
                    .BindAsync(_ => outbox.AddAsync(session, outbox.Create(EventTypes.PermissionsChanged, new JsonObject { ["userId"] = id.ToString() },
                        NotificationAudience.ForUser(id)) with { Title = "shell:notification.permissionsChanged" }, ct))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "user.roles.set", "user", id.ToString(),
                        details: new JsonObject { ["roles"] = string.Join(",", roles.Select(r => r.Name)) }, ct: ct)), ct: ct))
            .BindAsync(_ => repository.FindAsync(id, ct));
}

/// <summary>Sets a new temporary password.</summary>
public sealed class ResetPassword(UserRepository repository, AuditLog audit, RequestContext context)
{
    public Task<Result<ResetPasswordResponse>> ExecuteAsync(Guid id, CancellationToken ct) =>
        Steps.Authorize(context.User, UserPermissions.Edit)
            .BindAsync(_ => repository.FindAsync(id, ct))
            .BindAsync(_ =>
            {
                var temporary = CreateUser.TemporaryPassword();
                return repository.SetPasswordAsync(id, PasswordHasher.Hash(temporary), ct).Map(_ => new ResetPasswordResponse(temporary));
            })
            .TapAsync(_ => audit.WriteAsync(context.User, "user.passwordReset", "user", id.ToString(), ct: ct));
}
