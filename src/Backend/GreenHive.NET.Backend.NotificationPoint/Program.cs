using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.NotificationPoint;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("notification-point");
builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<NotificationHub>();
builder.Services.AddSingleton<SubscriberForwarder>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SubscriberForwarder>());
builder.Services.AddSingleton<WnsForwarder>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WnsForwarder>());
builder.Services.AddSingleton<EventBusConsumer>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/notifications");

// Server-sent events; catch-up with the Last-Event-ID header or ?after=<sequence>.
api.MapGet("/stream", async (HttpContext http, NotificationHub hub, RequestContext context, long? after) =>
{
    var lastEventId = http.Request.Headers["Last-Event-ID"].ToString();
    var from = long.TryParse(lastEventId, out var parsed) ? parsed : after ?? 0;
    await hub.StreamAsync(http, context.User, from, http.RequestAborted);
});

api.MapGet("/", (EventStore store, RequestContext context, long? after, int? take, CancellationToken ct) =>
    store.AfterAsync(context.User, after ?? 0, Math.Clamp(take ?? 50, 1, 200), ct).ToHttp());

api.MapGet("/status", (NotificationHub hub, EventStore store, CancellationToken ct) =>
    store.LatestSequenceAsync(ct).Map(sequence => new { connections = hub.ConnectionCount, latestSequence = sequence }).ToHttp());

api.MapPost("/channels", (ChannelRegistration registration, WnsForwarder wns, RequestContext context, CancellationToken ct) =>
    wns.RegisterChannelAsync(context.User.Id, registration.ChannelUri, ct).ToUnit().ToHttp());

app.MapInternal().MapPost("/events", (NotificationEnvelope[] envelopes, EventBusConsumer consumer, CancellationToken ct) =>
    consumer.IngestAsync(envelopes, ct).Map(accepted => new { accepted }).ToHttp());

app.Run();

internal sealed record ChannelRegistration(string ChannelUri);
