using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.ConfigurationService;

/// <summary>Settings per tenant and per user; user settings override tenant settings with the same key.</summary>
public sealed class SettingsStore(Db db, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<IReadOnlyList<ConfigSetting>>> ListAsync(CancellationToken ct) =>
        Steps.Authenticated(context.User).BindAsync(user => db.QueryAsync(
            """
            select scope, key, value::text as value, updated_at, updated_by from config.settings
            where tenant_id = @tenant and (scope = 'tenant' or (scope = 'user' and owner = @owner))
            order by key, scope
            """, Map, new { tenant = user.TenantId, owner = user.Id.ToString() }, ct));

    public Task<Result<ConfigSetting>> GetAsync(string key, CancellationToken ct) =>
        Steps.Authenticated(context.User).BindAsync(user => Effective(user.TenantId, user.Id, key, ct));

    /// <summary>Effective value: the user's own setting or the tenant default.</summary>
    public Task<Result<ConfigSetting>> Effective(Guid tenant, Guid? userId, string key, CancellationToken ct) =>
        db.FindAsync(
            """
            select scope, key, value::text as value, updated_at, updated_by from config.settings
            where tenant_id = @tenant and key = @key and (scope = 'tenant' or (scope = 'user' and owner = @owner))
            order by case scope when 'user' then 0 else 1 end limit 1
            """, Map, new { tenant, key, owner = userId?.ToString() ?? string.Empty }, Steps.NotFound("config.setting", key), ct);

    public Task<Result<ConfigSetting>> SetAsync(string scope, string key, JsonElement value, CancellationToken ct) =>
        Steps.Validate((scope, key), r => Validation.Combine(Validation.OneOf("scope", r.scope, "tenant", "user"), Validation.RequiredMax("key", r.key, 100)))
            .Bind(valid => valid.Item1 == "tenant" ? Steps.Authorize(context.User, "settings.edit").Map(_ => valid) : Steps.Authenticated(context.User).Map(_ => valid))
            .BindAsync(valid => db.InTransactionAsync(session =>
                session.ExecuteAsync(
                        """
                        insert into config.settings (tenant_id, scope, owner, key, value, updated_at, updated_by)
                        values (@tenant, @scope, @owner, @key, @value::jsonb, @now, @by)
                        on conflict (tenant_id, scope, owner, key) do update set value = excluded.value, updated_at = excluded.updated_at, updated_by = excluded.updated_by
                        """,
                        new
                        {
                            tenant = context.User.TenantId,
                            scope = valid.Item1,
                            owner = valid.Item1 == "user" ? context.User.Id.ToString() : string.Empty,
                            key = valid.Item2,
                            value = value.GetRawText(),
                            now = context.Now,
                            by = context.User.UserName,
                        }, ct)
                    .BindAsync(_ => valid.Item1 == "tenant"
                        ? outbox.AddAsync(session, outbox.Create("settings.changed", new JsonObject { ["key"] = valid.Item2 }), ct)
                        : Task.FromResult(Result.Success()))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, "setting.set", "setting", $"{valid.Item1}:{valid.Item2}", ct: ct)), ct: ct)
                .Map(_ => new ConfigSetting(valid.Item1, valid.Item2, value) { UpdatedAt = context.Now, UpdatedBy = context.User.UserName }));

    public Task<Result<Unit>> DeleteAsync(string scope, string key, CancellationToken ct) =>
        (scope == "tenant" ? Steps.Authorize(context.User, "settings.edit") : Steps.Authenticated(context.User).ToUnit())
            .BindAsync(_ => db.ExecuteAsync("delete from config.settings where tenant_id = @tenant and scope = @scope and owner = @owner and key = @key",
                new { tenant = context.User.TenantId, scope, owner = scope == "user" ? context.User.Id.ToString() : string.Empty, key }, ct))
            .Bind(rows => rows > 0 ? Result.Success() : Result.Failure(Steps.NotFound("config.setting", key)));

    private static ConfigSetting Map(System.Data.Common.DbDataReader r) =>
        new(r.Get<string>("scope"), r.Get<string>("key"), JsonDocument.Parse(r.Get<string>("value")).RootElement.Clone())
        {
            UpdatedAt = r.GetTime("updated_at"),
            UpdatedBy = r.GetOrNull<string>("updated_by"),
        };
}
