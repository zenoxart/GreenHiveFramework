using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Contracts;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.NotificationPoint;

/// <summary>Live client connections (server-sent events). Each connection gets the events its user may see.</summary>
public sealed class NotificationHub(EventStore store, ILogger<NotificationHub> logger)
{
    private readonly ConcurrentDictionary<Guid, Connection> _connections = new();

    public int ConnectionCount => _connections.Count;

    public bool IsOnline(Guid userId) => _connections.Values.Any(c => c.User.Id == userId);

    public void Publish(NotificationEnvelope envelope)
    {
        foreach (var connection in _connections.Values)
        {
            if (Audience.CanSee(connection.User, envelope))
            {
                connection.Channel.Writer.TryWrite(envelope);
            }
        }
    }

    /// <summary>Streams events to one client until it disconnects: catch-up from the database first, then live events.</summary>
    public async Task StreamAsync(HttpContext http, GreenHiveUser user, long after, CancellationToken ct)
    {
        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var connection = new Connection(user, Channel.CreateBounded<NotificationEnvelope>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        }));
        var id = Guid.NewGuid();
        _connections[id] = connection;
        logger.LogInformation("{User} connected ({Count} connections)", user.UserName, _connections.Count);
        try
        {
            var lastSent = after;
            await http.Response.WriteAsync($"retry: 5000\n: connected\n\n", ct);
            await http.Response.Body.FlushAsync(ct);

            if (after > 0)
            {
                var missed = await store.AfterAsync(user, after, 200, ct);
                foreach (var envelope in missed.GetValueOrDefault([]))
                {
                    await WriteAsync(http, envelope, ct);
                    lastSent = envelope.Sequence;
                }
            }

            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(20));
            var heartbeatTask = heartbeat.WaitForNextTickAsync(ct).AsTask();
            while (!ct.IsCancellationRequested)
            {
                var readTask = connection.Channel.Reader.WaitToReadAsync(ct).AsTask();
                var finished = await Task.WhenAny(readTask, heartbeatTask);
                if (finished == heartbeatTask)
                {
                    await http.Response.WriteAsync(": ping\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                    heartbeatTask = heartbeat.WaitForNextTickAsync(ct).AsTask();
                    continue;
                }

                if (!await readTask)
                {
                    break;
                }

                while (connection.Channel.Reader.TryRead(out var envelope))
                {
                    if (envelope.Sequence > lastSent)
                    {
                        await WriteAsync(http, envelope, ct);
                        lastSent = envelope.Sequence;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _connections.TryRemove(id, out _);
            logger.LogInformation("{User} disconnected ({Count} connections)", user.UserName, _connections.Count);
        }
    }

    private static async Task WriteAsync(HttpContext http, NotificationEnvelope envelope, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(envelope, GreenHiveJson.Options);
        await http.Response.WriteAsync($"id: {envelope.Sequence}\nevent: {envelope.Type}\ndata: {json}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    private sealed record Connection(GreenHiveUser User, Channel<NotificationEnvelope> Channel);
}
