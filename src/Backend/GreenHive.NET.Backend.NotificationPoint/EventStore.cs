using System.Text.Json;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.NotificationPoint;

/// <summary>Stores events with a sequence number (de-duplicated by event ID) and answers catch-up queries.</summary>
public sealed class EventStore(Db db)
{
    /// <summary>Stores the event; returns it with its sequence, or None if it was already stored (duplicate).</summary>
    public Task<Result<Option<NotificationEnvelope>>> AppendAsync(NotificationEnvelope envelope, CancellationToken ct) =>
        db.InTransactionAsync(async session =>
        {
            var inserted = await session.QuerySingleAsync(
                """
                insert into notify.events (id, type, source, tenant_id, audience_kind, audience_value, severity, envelope, created_at)
                values (@id, @type, @source, @tenantId, @audienceKind, @audienceValue, @severity, @envelope::jsonb, @createdAt)
                on conflict (id) do nothing
                returning sequence
                """,
                r => r.Get<long>("sequence"),
                new
                {
                    id = envelope.Id,
                    type = envelope.Type,
                    source = envelope.Source,
                    tenantId = envelope.TenantId,
                    audienceKind = envelope.Audience.Kind,
                    audienceValue = envelope.Audience.Value,
                    severity = envelope.Severity,
                    envelope = JsonSerializer.Serialize(envelope, GreenHiveJson.Options),
                    createdAt = envelope.Timestamp == default ? DateTimeOffset.UtcNow : envelope.Timestamp,
                }, ct);
            return inserted.Map(sequence => sequence.Map(s => envelope with { Sequence = s }));
        }, ct: ct);

    /// <summary>Events after a sequence number that the user may see (catch-up after reconnect), oldest first.</summary>
    public Task<Result<IReadOnlyList<NotificationEnvelope>>> AfterAsync(GreenHiveUser user, long after, int take, CancellationToken ct) =>
        db.QueryAsync(
                """
                select sequence, envelope::text as envelope from notify.events
                where tenant_id = @tenant and sequence > @after and audience_kind <> 'System'
                  and created_at > now() - interval '7 days'
                order by sequence desc limit 500
                """,
                r => JsonSerializer.Deserialize<NotificationEnvelope>(r.Get<string>("envelope"), GreenHiveJson.Options)! with { Sequence = r.Get<long>("sequence") },
                new { tenant = user.TenantId, after }, ct)
            .Map(list => (IReadOnlyList<NotificationEnvelope>)list.Where(e => Audience.CanSee(user, e)).Take(take).OrderBy(e => e.Sequence).ToList());

    public Task<Result<long>> LatestSequenceAsync(CancellationToken ct) =>
        db.ScalarAsync<long>("select coalesce(max(sequence), 0) from notify.events", null, ct).Map(v => v);
}

/// <summary>Audience rules: who may see an event.</summary>
public static class Audience
{
    public static bool CanSee(GreenHiveUser user, NotificationEnvelope envelope) =>
        envelope.TenantId == user.TenantId && envelope.Audience.Kind switch
        {
            AudienceKind.Tenant => true,
            AudienceKind.User => Guid.TryParse(envelope.Audience.Value, out var id) && id == user.Id,
            AudienceKind.Permission => envelope.Audience.Value is { } permission && user.Has(permission),
            _ => false,
        };
}
