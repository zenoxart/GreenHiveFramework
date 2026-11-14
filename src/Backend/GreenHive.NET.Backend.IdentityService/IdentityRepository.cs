using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.IdentityService;

/// <summary>Account record including the password hash (never leaves the service).</summary>
public sealed record Account(UserInfo Info, string PasswordHash);

/// <summary>Data access of the identity schema (adapter, no business rules).</summary>
public sealed class IdentityRepository(Db db)
{
    private const string UserColumns = """
        u.id, u.tenant_id, u.user_name, u.display_name, u.email, u.password_hash, u.is_active, u.approval_limit, u.last_login_at,
        coalesce((select array_agg(r.name order by r.name) from identity.user_roles ur join identity.roles r on r.id = ur.role_id where ur.user_id = u.id), '{}') as roles,
        coalesce((select array_agg(distinct rp.permission order by rp.permission) from identity.user_roles ur
                  join identity.role_permissions rp on rp.role_id = ur.role_id where ur.user_id = u.id), '{}') as permissions
        """;

    public Task<Result<Option<Account>>> FindByNameAsync(string userName, CancellationToken ct) =>
        db.QuerySingleAsync($"select {UserColumns} from identity.users u where lower(u.user_name) = lower(@userName) or lower(u.email) = lower(@userName)",
            MapAccount, new { userName }, ct);

    public Task<Result<Option<Account>>> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.QuerySingleAsync($"select {UserColumns} from identity.users u where u.id = @id", MapAccount, new { id }, ct);

    public Task<Result<Option<AvatarInfo>>> AvatarAsync(Guid userId, CancellationToken ct) =>
        db.QuerySingleAsync("select content_type, data_base64, updated_at from identity.user_avatars where user_id = @userId",
            r => new AvatarInfo(r.Get<string>("content_type"), r.Get<string>("data_base64")) { UpdatedAt = r.Get<DateTimeOffset>("updated_at") },
            new { userId }, ct);

    public Task<Result<Unit>> SaveAvatarAsync(Guid userId, AvatarInfo avatar, CancellationToken ct) =>
        db.ExecuteAsync("""
            insert into identity.user_avatars (user_id, content_type, data_base64, updated_at) values (@userId, @contentType, @data, @at)
            on conflict (user_id) do update set content_type = excluded.content_type, data_base64 = excluded.data_base64, updated_at = excluded.updated_at
            """, new { userId, contentType = avatar.ContentType, data = avatar.DataBase64, at = avatar.UpdatedAt }, ct).ToUnit();

    public Task<Result<Unit>> DeleteAvatarAsync(Guid userId, CancellationToken ct) =>
        db.ExecuteAsync("delete from identity.user_avatars where user_id = @userId", new { userId }, ct).ToUnit();

    public Task<Result<Unit>> SetPasswordHashAsync(Guid userId, string hash, CancellationToken ct) =>
        db.ExecuteAsync("update identity.users set password_hash = @hash where id = @userId", new { userId, hash }, ct).ToUnit();

    public Task<Result<Unit>> TouchLoginAsync(Guid id, DateTimeOffset at, CancellationToken ct) =>
        db.ExecuteAsync("update identity.users set last_login_at = @at where id = @id", new { id, at }, ct).ToUnit();

    public Task<Result<IReadOnlyList<PermissionInfo>>> PermissionsAsync(CancellationToken ct) =>
        db.QueryAsync("select name, module_id, description from identity.permissions order by module_id, name",
            r => new PermissionInfo(r.Get<string>("name"), r.Get<string>("module_id"), r.Get<string>("description")), null, ct);

    public Task<Result<IReadOnlyList<RoleInfo>>> RolesAsync(Guid tenantId, CancellationToken ct) =>
        db.QueryAsync(
            """
            select r.id, r.name, r.description,
                   coalesce((select array_agg(permission order by permission) from identity.role_permissions where role_id = r.id), '{}') as permissions,
                   (select count(*)::int from identity.user_roles where role_id = r.id) as user_count
            from identity.roles r where r.tenant_id = @tenantId order by r.name
            """,
            r => new RoleInfo(r.Get<Guid>("id"), r.Get<string>("name"), r.Get<string>("description"))
            {
                Permissions = r.GetTextArray("permissions"),
                UserCount = r.Get<int>("user_count"),
            }, new { tenantId }, ct);

    public Task<Result<Unit>> SetRolePermissionsAsync(DbSession session, Guid roleId, IReadOnlyList<string> permissions, CancellationToken ct) =>
        session.ExecuteAsync("delete from identity.role_permissions where role_id = @roleId", new { roleId }, ct)
            .BindAsync(_ => session.ExecuteAsync(
                "insert into identity.role_permissions (role_id, permission) select @roleId, p from unnest(@permissions) p",
                new { roleId, permissions = permissions.ToArray() }, ct))
            .ToUnit();

    public Task<Result<Unit>> RegisterPermissionsAsync(RegisterPermissionsRequest request, CancellationToken ct) =>
        db.InTransactionAsync(async session =>
        {
            foreach (var permission in request.Permissions)
            {
                var result = await session.ExecuteAsync(
                    """
                    insert into identity.permissions (name, module_id, description) values (@name, @moduleId, @description)
                    on conflict (name) do update set module_id = excluded.module_id, description = excluded.description
                    """, new { name = permission.Name, moduleId = request.ModuleId, description = permission.Description }, ct);
                if (result.IsFailure)
                {
                    return result.ToUnit();
                }
            }

            return Result.Success();
        }, ct: ct);

    private static Account MapAccount(System.Data.Common.DbDataReader r) => new(
        new UserInfo(r.Get<Guid>("id"), r.Get<string>("user_name"), r.Get<string>("display_name"), r.Get<string>("email"), r.Get<Guid>("tenant_id"))
        {
            Roles = r.GetTextArray("roles"),
            Permissions = r.GetTextArray("permissions"),
            ApprovalLimit = r.Get<decimal>("approval_limit"),
            IsActive = r.Get<bool>("is_active"),
            LastLoginAt = r.GetTimeOrNull("last_login_at"),
        },
        r.Get<string>("password_hash"));
}
