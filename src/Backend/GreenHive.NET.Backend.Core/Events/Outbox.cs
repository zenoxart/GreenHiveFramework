using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenHive.NET.Backend.Core.Events;

/// <summary>
/// OutboxStep: writes domain events into platform.outbox inside the use case's transaction.
/// The <see cref="OutboxDispatcher"/> forwards them to the NotificationPoint afterwards (at least once).
/// </summary>
public sealed class OutboxWriter(IOptions<GreenHiveOptions> options, TimeProvider time)
{
    public NotificationEnvelope Create(string type, JsonObject? payload, NotificationAudience? audience = null, Guid? tenantId = null) => new()
    {
        Type = type,
        Source = options.Value.ServiceName,
        Timestamp = time.GetUtcNow(),
        TenantId = tenantId ?? TenantId.Demo.Value,
        Audience = audience ?? NotificationAudience.Everyone,
        Payload = payload,
    };

    public Task<Result<Unit>> AddAsync(DbSession session, NotificationEnvelope envelope, CancellationToken ct = default)
    {
        var complete = envelope with
        {
            Source = string.IsNullOrEmpty(envelope.Source) ? options.Value.ServiceName : envelope.Source,
            Timestamp = envelope.Timestamp == default ? time.GetUtcNow() : envelope.Timestamp,
            TenantId = envelope.TenantId == Guid.Empty ? TenantId.Demo.Value : envelope.TenantId,
        };
        return session.ExecuteAsync(
            "insert into platform.outbox (id, source, type, envelope, created_at) values (@id, @source, @type, @envelope::jsonb, @createdAt)",
            new
            {
                id = complete.Id,
                source = options.Value.ServiceName,
                type = complete.Type,
                envelope = JsonSerializer.Serialize(complete, GreenHiveJson.Options),
                createdAt = complete.Timestamp,
            }, ct).ToUnit();
    }
}

/// <summary>Forwards undispatched outbox rows of this service to the NotificationPoint (POST /internal/events).</summary>
public sealed class OutboxDispatcher(Db db, ServiceClient client, IOptions<GreenHiveOptions> options, ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableOutbox || options.Value.ServiceUrl("notification-point") is null)
        {
            logger.LogInformation("Outbox dispatcher disabled (no notification-point configured).");
            return;
        }

        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await DispatchBatchAsync(stoppingToken);
            delay = result.Match(count => count > 0 ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(1), error =>
            {
                logger.LogWarning("Outbox dispatch failed: {Error}", error);
                return TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
            });
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private Task<Result<int>> DispatchBatchAsync(CancellationToken ct) =>
        db.InTransactionAsync(async session =>
        {
            var rows = await session.QueryAsync(
                """
                select id, envelope::text as envelope from platform.outbox
                where source = @source and dispatched_at is null
                order by created_at limit 50 for update skip locked
                """,
                r => (Id: r.Get<Guid>("id"), Envelope: r.Get<string>("envelope")), new { source = options.Value.ServiceName }, ct);
            if (!rows.TryGetValue(out var list, out var error))
            {
                return Result<int>.Failure(error);
            }

            if (list.Count == 0)
            {
                return Result.Success(0);
            }

            var envelopes = list.Select(r => JsonSerializer.Deserialize<NotificationEnvelope>(r.Envelope, GreenHiveJson.Options)!).ToList();
            var sent = await client.PostAsync<JsonElement?>("notification-point", "/internal/events", envelopes, null, ct);
            if (sent.IsFailure)
            {
                return Result<int>.Failure(sent.Error);
            }

            var ids = list.Select(r => r.Id).ToArray();
            return await session.ExecuteAsync("update platform.outbox set dispatched_at = now() where id = any(@ids)", new { ids }, ct);
        }, ct: ct);
}
