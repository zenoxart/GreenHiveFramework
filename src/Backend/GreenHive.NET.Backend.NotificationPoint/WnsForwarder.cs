using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.NotificationPoint;

/// <summary>
/// Windows push (WNS) for users without a live connection. Active only when GreenHive__Wns__ClientId and
/// GreenHive__Wns__ClientSecret (Azure app registration / Partner Center) are configured; otherwise events
/// are delivered when the client reconnects (catch-up by sequence).
/// </summary>
public sealed class WnsForwarder(Db db, NotificationHub hub, IHttpClientFactory http, IConfiguration configuration, ILogger<WnsForwarder> logger)
    : BackgroundService
{
    private readonly Channel<NotificationEnvelope> _queue = Channel.CreateUnbounded<NotificationEnvelope>();
    private string? _token;
    private DateTimeOffset _tokenExpires;

    private string? ClientId => configuration["GreenHive:Wns:ClientId"];

    private string? ClientSecret => configuration["GreenHive:Wns:ClientSecret"];

    public bool Enabled => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);

    public void Enqueue(NotificationEnvelope envelope)
    {
        if (Enabled && envelope.Title is not null && envelope.Audience.Kind == AudienceKind.User)
        {
            _queue.Writer.TryWrite(envelope);
        }
    }

    public Task<Result<int>> RegisterChannelAsync(Guid userId, string channelUri, CancellationToken ct) =>
        !Uri.TryCreate(channelUri, UriKind.Absolute, out var uri) || !uri.Host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase)
            ? Task.FromResult(Result<int>.Failure(Error.Validation("notifications.channel.invalid", "notifications:channel.invalid")))
            : db.ExecuteAsync(
                """
                insert into notify.channels (user_id, channel_uri, updated_at) values (@userId, @channelUri, now())
                on conflict (user_id, channel_uri) do update set updated_at = now()
                """, new { userId, channelUri }, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var envelope in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!Guid.TryParse(envelope.Audience.Value, out var userId) || hub.IsOnline(userId))
            {
                continue;
            }

            var channels = await db.QueryAsync("select channel_uri from notify.channels where user_id = @userId and updated_at > now() - interval '30 days'",
                r => r.Get<string>("channel_uri"), new { userId }, stoppingToken);
            foreach (var channel in channels.GetValueOrDefault([]))
            {
                await SendAsync(channel, envelope, stoppingToken);
            }
        }
    }

    private async Task SendAsync(string channelUri, NotificationEnvelope envelope, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient("greenhive");
            if (_token is null || _tokenExpires < DateTimeOffset.UtcNow.AddMinutes(1))
            {
                var response = await client.PostAsync("https://login.live.com/accesstoken.srf", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = ClientId!,
                    ["client_secret"] = ClientSecret!,
                    ["scope"] = "notify.windows.com",
                }), ct);
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                _token = doc.RootElement.GetProperty("access_token").GetString();
                _tokenExpires = DateTimeOffset.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32());
            }

            var request = new HttpRequestMessage(HttpMethod.Post, channelUri)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, GreenHiveJson.Options))),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Add("X-WNS-Type", "wns/raw");
            var sent = await client.SendAsync(request, ct);
            if (!sent.IsSuccessStatusCode)
            {
                logger.LogWarning("WNS push failed with {Status}", sent.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "WNS push failed");
        }
    }
}
