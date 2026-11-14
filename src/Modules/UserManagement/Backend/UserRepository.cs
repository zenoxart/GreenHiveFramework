using System.Data.Common;
using GreenHive.NET.Modules.UserManagement.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Modules.UserManagement.Backend;

/// <summary>Accounts (identity schema) joined with profiles and departments (usermgmt schema).</summary>
public sealed class UserRepository(Db db)
{
    private const string Select = """
        select u.id, u.user_name, u.display_name, u.email, u.is_active, u.approval_limit, u.last_login_at,
               p.department_id, d.name as department_name, coalesce(p.job_title, '') as job_title, coalesce(p.phone, '') as phone,
               coalesce(p.location, '') as location, p.manager_id, m.display_name as manager_name, p.hired_on, coalesce(p.row_version, 1) as row_version,
               coalesce((select array_agg(r.name order by r.name) from identity.user_roles ur join identity.roles r on r.id = ur.role_id where ur.user_id = u.id), '{}') as roles,
               count(*) over () as total
        from identity.users u
        left join usermgmt.profiles p on p.user_id = u.id
        left join usermgmt.departments d on d.id = p.department_id
        left join identity.users m on m.id = p.manager_id
        """;

    public Task<Result<PagedResult<UserDto>>> SearchAsync(Guid tenantId, string? search, Guid? departmentId, bool includeInactive, int skip, int take, CancellationToken ct) =>
        db.QueryAsync(
                $"""
                {Select}
                where u.tenant_id = @tenantId and (@includeInactive or u.is_active)
                  and (@departmentId::uuid is null or p.department_id = @departmentId)
                  and (@search::text is null or u.display_name ilike '%' || @search || '%' or u.user_name ilike '%' || @search || '%' or u.email ilike '%' || @search || '%')
                order by u.display_name offset @skip limit @take
                """,
                r => (User: Map(r), Total: (int)r.Get<long>("total")),
                new { tenantId, search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), departmentId, includeInactive, skip, take }, ct)
            .Map(rows => new PagedResult<UserDto>(rows.Select(r => r.User).ToList(), rows.FirstOrDefault().Total, skip, take));

    public Task<Result<UserDto>> FindAsync(Guid id, CancellationToken ct) =>
        db.FindAsync($"{Select} where u.id = @id", Map, new { id }, Error.NotFound("users.user.notFound", "users:user.notFound").WithMetadata("id", id), ct);

    public Task<Result<bool>> ExistsAsync(string userName, string email, CancellationToken ct) =>
        db.ScalarAsync<bool>("select exists(select 1 from identity.users where lower(user_name) = lower(@userName) or lower(email) = lower(@email))",
            new { userName, email }, ct).Map(v => v);

    public Task<Result<IReadOnlyList<(Guid Id, string Name)>>> RolesByNameAsync(Guid tenantId, IReadOnlyList<string> names, CancellationToken ct) =>
        db.QueryAsync("select id, name from identity.roles where tenant_id = @tenantId and lower(name) = any(@names)",
            r => (r.Get<Guid>("id"), r.Get<string>("name")), new { tenantId, names = names.Select(n => n.ToLowerInvariant()).ToArray() }, ct);

    public Task<Result<Unit>> InsertAsync(DbSession session, UserDto user, Guid tenantId, string passwordHash, IReadOnlyList<Guid> roleIds, CancellationToken ct) =>
        session.ExecuteAsync(
                """
                insert into identity.users (id, tenant_id, user_name, display_name, email, password_hash, is_active, approval_limit, created_at)
                values (@id, @tenantId, @userName, @displayName, @email, @hash, true, @limit, now())
                """,
                new { id = user.Id, tenantId, userName = user.UserName, displayName = user.DisplayName, email = user.Email, hash = passwordHash, limit = user.ApprovalLimit }, ct)
            .BindAsync(_ => session.ExecuteAsync(
                """
                insert into usermgmt.profiles (user_id, department_id, job_title, phone, location, hired_on)
                values (@id, @departmentId, @jobTitle, @phone, @location, current_date)
                """,
                new { id = user.Id, departmentId = user.DepartmentId, jobTitle = user.JobTitle, phone = user.Phone, location = user.Location }, ct))
            .BindAsync(_ => SetRolesAsync(session, user.Id, roleIds, ct));

    public Task<Result<Unit>> UpdateAsync(DbSession session, UserDto user, int expectedRowVersion, CancellationToken ct) =>
        session.ExecuteAsync(
                "update identity.users set display_name = @displayName, email = @email, is_active = @isActive, approval_limit = @limit where id = @id",
                new { id = user.Id, displayName = user.DisplayName, email = user.Email, isActive = user.IsActive, limit = user.ApprovalLimit }, ct)
            .BindAsync(_ => session.ExecuteAsync(
                """
                insert into usermgmt.profiles (user_id, department_id, job_title, phone, location, manager_id, row_version)
                values (@id, @departmentId, @jobTitle, @phone, @location, @managerId, 1)
                on conflict (user_id) do update set department_id = excluded.department_id, job_title = excluded.job_title, phone = excluded.phone,
                    location = excluded.location, manager_id = excluded.manager_id, row_version = usermgmt.profiles.row_version + 1
                where usermgmt.profiles.row_version = @expected
                """,
                new
                {
                    id = user.Id, departmentId = user.DepartmentId, jobTitle = user.JobTitle, phone = user.Phone, location = user.Location,
                    managerId = user.ManagerId, expected = expectedRowVersion,
                }, ct))
            .Bind(rows => rows == 1 ? Result.Success() : Result.Failure(Error.Conflict("users.user.concurrency", "users:user.changedByOthers")));

    public Task<Result<Unit>> SetRolesAsync(DbSession session, Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct) =>
        session.ExecuteAsync("delete from identity.user_roles where user_id = @userId", new { userId }, ct)
            .BindAsync(_ => session.ExecuteAsync("insert into identity.user_roles (user_id, role_id) select @userId, r from unnest(@roleIds) r",
                new { userId, roleIds = roleIds.ToArray() }, ct))
            .ToUnit();

    public Task<Result<int>> SetPasswordAsync(Guid userId, string hash, CancellationToken ct) =>
        db.ExecuteAsync("update identity.users set password_hash = @hash where id = @userId", new { userId, hash }, ct);

    public Task<Result<IReadOnlyList<DepartmentDto>>> DepartmentsAsync(Guid tenantId, CancellationToken ct) =>
        db.QueryAsync(
            """
            select d.id, d.name, d.cost_center, (select count(*)::int from usermgmt.profiles p join identity.users u on u.id = p.user_id
                                                 where p.department_id = d.id and u.is_active) as users
            from usermgmt.departments d where d.tenant_id = @tenantId order by d.name
            """,
            r => new DepartmentDto(r.Get<Guid>("id"), r.Get<string>("name"), r.Get<string>("cost_center"), r.Get<int>("users")), new { tenantId }, ct);

    public Task<Result<IReadOnlyList<RoleSummary>>> RolesAsync(Guid tenantId, CancellationToken ct) =>
        db.QueryAsync(
            "select r.name, r.description, (select count(*)::int from identity.user_roles where role_id = r.id) as users from identity.roles r where r.tenant_id = @tenantId order by r.name",
            r => new RoleSummary(r.Get<string>("name"), r.Get<string>("description"), r.Get<int>("users")), new { tenantId }, ct);

    /// <summary>Active users that have a permission (directly or through "*") - lookups for approver fields.</summary>
    public Task<Result<IReadOnlyList<(Guid Id, string Name)>>> WithPermissionAsync(Guid tenantId, string? permission, CancellationToken ct) =>
        db.QueryAsync(
            """
            select distinct u.id, u.display_name from identity.users u
            left join identity.user_roles ur on ur.user_id = u.id
            left join identity.role_permissions rp on rp.role_id = ur.role_id
            where u.tenant_id = @tenantId and u.is_active and (@permission::text is null or rp.permission in (@permission, '*'))
            order by u.display_name
            """,
            r => (r.Get<Guid>("id"), r.Get<string>("display_name")), new { tenantId, permission }, ct);

    private static UserDto Map(DbDataReader r) =>
        new(r.Get<Guid>("id"), r.Get<string>("user_name"), r.Get<string>("display_name"), r.Get<string>("email"), r.Get<bool>("is_active"))
        {
            DepartmentId = r.GetNullable<Guid>("department_id"),
            DepartmentName = r.GetOrNull<string>("department_name"),
            JobTitle = r.Get<string>("job_title"),
            Phone = r.Get<string>("phone"),
            Location = r.Get<string>("location"),
            ManagerId = r.GetNullable<Guid>("manager_id"),
            ManagerName = r.GetOrNull<string>("manager_name"),
            HiredOn = r.GetNullable<DateOnly>("hired_on"),
            Roles = r.GetTextArray("roles"),
            ApprovalLimit = r.Get<decimal>("approval_limit"),
            LastLoginAt = r.GetTimeOrNull("last_login_at"),
            RowVersion = r.Get<int>("row_version"),
        };
}
