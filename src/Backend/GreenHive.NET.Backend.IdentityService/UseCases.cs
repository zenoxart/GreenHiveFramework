using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.IdentityService;

/// <summary>Login: validate -> load account -> decide (active, password) -> issue token -> record login -> project.</summary>
public sealed class Login(IdentityRepository accounts, TokenService tokens, AuditLog audit, RequestContext context)
{
    private static readonly Error InvalidCredentials = Error.Unauthorized("identity.login.invalid", "identity:login.invalid");

    public Task<Result<LoginResponse>> ExecuteAsync(LoginRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(Validation.Required("userName", r.UserName), Validation.Required("password", r.Password)))
            .BindAsync(valid => accounts.FindByNameAsync(valid.Item1, ct).Bind(account => account.ToResult(InvalidCredentials)))
            .Bind(account => Decide(account, request.Password))
            .TapAsync(account => accounts.TouchLoginAsync(account.Info.Id, context.Now, ct))
            .TapAsync(account => audit.WriteAsync(ToUser(account.Info), "login", "user", account.Info.Id.ToString(), ct: ct))
            .Map(account =>
            {
                var token = tokens.Issue(ToUser(account.Info));
                return new LoginResponse(token.AccessToken, token.ExpiresAt, account.Info with { LastLoginAt = context.Now });
            });

    /// <summary>Pure decision on the loaded account.</summary>
    private static Result<Account> Decide(Account account, string password) =>
        !account.Info.IsActive ? Error.Forbidden("identity.login.inactive", "identity:login.inactive")
        : !PasswordHasher.Verify(password, account.PasswordHash) ? InvalidCredentials
        : account;

    public static GreenHiveUser ToUser(UserInfo info, string? actor = null) =>
        new(info.Id, info.UserName, info.DisplayName, info.TenantId, info.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase)) { Actor = actor };
}

/// <summary>The signed-in user with current roles and permissions (permissions may have changed since login).</summary>
public sealed class GetCurrentUser(IdentityRepository accounts, RequestContext context)
{
    public Task<Result<UserInfo>> ExecuteAsync(CancellationToken ct) =>
        Steps.Authenticated(context.User)
            .BindAsync(user => accounts.FindByIdAsync(user.Id, ct))
            .Bind(account => account.ToResult(Steps.NotFound("identity.user", context.User.Id)))
            .Map(account => account.Info);
}

/// <summary>Issues a fresh token for the signed-in user (with current permissions).</summary>
public sealed class RefreshToken(IdentityRepository accounts, TokenService tokens, RequestContext context)
{
    public Task<Result<LoginResponse>> ExecuteAsync(CancellationToken ct) =>
        Steps.Authenticated(context.User)
            .BindAsync(user => accounts.FindByIdAsync(user.Id, ct))
            .Bind(account => account.ToResult(Steps.NotFound("identity.user", context.User.Id)))
            .Ensure(account => account.Info.IsActive, Error.Forbidden("identity.login.inactive", "identity:login.inactive"))
            .Map(account =>
            {
                var token = tokens.Issue(Login.ToUser(account.Info));
                return new LoginResponse(token.AccessToken, token.ExpiresAt, account.Info);
            });
}

/// <summary>
/// On-behalf-of token exchange: the workflow engine / AI agent acts for the RunAs user with exactly that user's
/// permissions, marked as delegated (actor) for auditing. Short-lived.
/// </summary>
public sealed class DelegateToken(IdentityRepository accounts, TokenService tokens, AuditLog audit)
{
    private static readonly string[] AllowedActors = ["workflow-engine", "ai-agent"];

    public Task<Result<LoginResponse>> ExecuteAsync(DelegateTokenRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => Validation.Combine(Validation.NotEmpty("userId", r.UserId), Validation.OneOf("actor", r.Actor, AllowedActors)))
            .BindAsync(valid => accounts.FindByIdAsync(valid.Item1, ct))
            .Bind(account => account.ToResult(Steps.NotFound("identity.user", request.UserId)))
            .Ensure(account => account.Info.IsActive, Error.Forbidden("identity.delegate.inactive", "identity:login.inactive"))
            .TapAsync(account => audit.WriteAsync(Login.ToUser(account.Info, request.Actor), "token.delegated", "user", account.Info.Id.ToString(), ct: ct))
            .Map(account =>
            {
                var token = tokens.Issue(Login.ToUser(account.Info, request.Actor), TimeSpan.FromMinutes(15));
                return new LoginResponse(token.AccessToken, token.ExpiresAt, account.Info);
            });
}

/// <summary>Replaces the permissions of a role and announces permissions.changed (clients refresh their token).</summary>
public sealed class SetRolePermissions(Db db, IdentityRepository repository, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<RoleInfo>> ExecuteAsync(Guid roleId, IReadOnlyList<string> permissions, CancellationToken ct) =>
        Steps.Authorize(context.User, "roles.edit")
            .BindAsync(_ => db.InTransactionAsync(session =>
                repository.SetRolePermissionsAsync(session, roleId, permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), ct)
                    .BindAsync(_ => outbox.AddAsync(session, outbox.Create(EventTypes.PermissionsChanged,
                        new JsonObject { ["roleId"] = roleId.ToString() }) with { Severity = NotificationSeverity.Info }, ct))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "role.permissions.set", "role", roleId.ToString(),
                        details: new JsonObject { ["permissions"] = string.Join(",", permissions) }, ct: ct)), ct: ct))
            .BindAsync(_ => repository.RolesAsync(context.User.TenantId, ct))
            .Bind(roles => roles.FirstOrNone(r => r.Id == roleId).ToResult(Steps.NotFound("identity.role", roleId)));
}

/// <summary>Profile picture of the signed-in user: validate (type, base64, size) -> store -> audit.</summary>
public sealed class SetAvatar(IdentityRepository accounts, AuditLog audit, RequestContext context)
{
    private static readonly string[] Types = ["image/png", "image/jpeg"];

    public Task<Result<AvatarInfo>> ExecuteAsync(AvatarInfo request, CancellationToken ct) =>
        Steps.Authenticated(context.User)
            .Bind(_ => Steps.Validate(request, r => Validation.Combine(
                Validation.OneOf("contentType", r.ContentType, Types),
                Validation.Required("dataBase64", r.DataBase64).Bind(DecodeImage))))
            .Map(_ => request with { UpdatedAt = context.Now })
            .BindAsync(avatar => accounts.SaveAvatarAsync(context.User.Id, avatar, ct).Map(_ => avatar))
            .TapAsync(_ => audit.WriteAsync(context.User, "avatar.changed", "user", context.User.Id.ToString(), ct: ct));

    private static Validation<byte[]> DecodeImage(string base64)
    {
        var buffer = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, buffer, out var length))
        {
            return Error.Field("dataBase64", "identity.avatar.invalid", "identity:avatar.invalid");
        }

        return length > AvatarInfo.MaxBytes
            ? Error.Field("dataBase64", "identity.avatar.tooLarge", "identity:avatar.tooLarge")
            : buffer[..length];
    }
}

/// <summary>The signed-in user changes the own password (current password required).</summary>
public sealed class ChangePassword(IdentityRepository accounts, AuditLog audit, RequestContext context)
{
    public Task<Result<Unit>> ExecuteAsync(ChangePasswordRequest request, CancellationToken ct) =>
        Steps.Authenticated(context.User)
            .Bind(_ => Steps.Validate(request, r => Validation.Combine(
                Validation.Required("currentPassword", r.CurrentPassword),
                Validation.Required("newPassword", r.NewPassword)
                    .Ensure(p => p.Length >= ChangePasswordRequest.MinLength, Error.Field("newPassword", "identity.password.tooShort", $"identity:password.tooShort|{ChangePasswordRequest.MinLength}"))
                    .Ensure(p => p != r.CurrentPassword, Error.Field("newPassword", "identity.password.unchanged", "identity:password.unchanged")))))
            .BindAsync(_ => accounts.FindByIdAsync(context.User.Id, ct))
            .Bind(account => account.ToResult(Steps.NotFound("identity.user", context.User.Id)))
            .Ensure(account => PasswordHasher.Verify(request.CurrentPassword, account.PasswordHash),
                Error.Field("currentPassword", "identity.password.wrong", "identity:password.wrong"))
            .BindAsync(account => accounts.SetPasswordHashAsync(account.Info.Id, PasswordHasher.Hash(request.NewPassword), ct))
            .TapAsync(_ => audit.WriteAsync(context.User, "password.changed", "user", context.User.Id.ToString(), ct: ct));
}
