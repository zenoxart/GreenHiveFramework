using System.Text.Json;
using System.Threading.Channels;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.NotificationPoint;

/// <summary>
/// Ingests events from the outboxes of all services: store (de-duplicated), push to live clients, queue for backend
/// subscribers (workflow triggers) and WNS push. This replaces a separate message broker for small installations.
/// </summary>
public sealed class EventBusConsumer(EventStore store, NotificationHub hub, SubscriberForwarder forwarder, WnsForwarder wns)
{
    public async Task<Result<int>> IngestAsync(IReadOnlyList<NotificationEnvelope> envelopes, CancellationToken ct)
    {
        var accepted = 0;
        foreach (var envelope in envelopes)
        {
            var stored = await store.AppendAsync(envelope, ct);
            if (!stored.TryGetValue(out var option, out var error))
            {
                return error;
            }

            if (!option.TryGetValue(out var withSequence))
            {
                continue; // duplicate delivery from an outbox retry
            }

            accepted++;
            hub.Publish(withSequence);
            forwarder.Enqueue(withSequence);
            wns.Enqueue(withSequence);
        }

        return accepted;
    }
}

/// <summary>Forwards every event to the configured backend subscribers (GreenHive__Subscribers__0=...), with retries.</summary>
public sealed class SubscriberForwarder(ServiceClient client, IConfiguration configuration, ILogger<SubscriberForwarder> logger) : BackgroundService
{
    private readonly Channel<NotificationEnvelope> _queue = Channel.CreateUnbounded<NotificationEnvelope>();

    private IReadOnlyList<string> Subscribers => configuration.GetSection("GreenHive:Subscribers").Get<string[]>() ?? [];

    public void Enqueue(NotificationEnvelope envelope)
    {
        if (Subscribers.Count > 0)
        {
            _queue.Writer.TryWrite(envelope);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var envelope in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var batch = new List<NotificationEnvelope> { envelope };
            while (batch.Count < 50 && _queue.Reader.TryRead(out var next))
            {
                batch.Add(next);
            }

            foreach (var subscriber in Subscribers)
            {
                for (var attempt = 1; attempt <= 5; attempt++)
                {
                    var sent = await client.PostAsync<JsonElement?>(subscriber, string.Empty, batch, null, stoppingToken);
                    if (sent.IsSuccess)
                    {
                        break;
                    }

                    logger.LogWarning("Forwarding {Count} events to {Subscriber} failed (attempt {Attempt}): {Error}", batch.Count, subscriber, attempt, sent.Error);
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), stoppingToken);
                }
            }
        }
    }
}
