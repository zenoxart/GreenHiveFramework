using System.Threading.RateLimiting;
using GreenHive.NET.Backend.ApiGateway;
using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Monads;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("api-gateway", useDatabase: false);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 200 * 1024 * 1024);
builder.Services.AddHttpClient("proxy", c => c.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = System.Net.DecompressionMethods.None,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });
builder.Services.AddSingleton<ReverseProxy>();
builder.Services.AddSingleton<RoutingConfiguration>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RoutingConfiguration>());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("GreenHive:RateLimitPerMinute", 1200),
            Window = TimeSpan.FromMinutes(1),
        }));
});

var app = builder.Build();
app.UseGreenHiveDefaults();
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new { service = "Green Hive API gateway", health = "/health/ready" })).AllowAnonymous();
app.MapGet("/api/gateway/routes", (RoutingConfiguration routing) => Results.Ok(routing.Routes)).RequireAuthorization();

// Everything else under /api is forwarded; /internal is never routed.
app.Map("/internal/{**rest}", () => ResultHttpMapper.Problem(Error.NotFound("gateway.route", "errors:notFound"))).AllowAnonymous();
app.Map("/api/{**rest}", async (HttpContext context, RoutingConfiguration routing, ReverseProxy proxy) =>
{
    if (routing.Routes.Count == 0)
    {
        await routing.RefreshAsync(context.RequestAborted);
    }

    var route = routing.Match(context.Request.Path);
    if (route is null)
    {
        // A module may have been published since the last refresh.
        await routing.RefreshAsync(context.RequestAborted);
        route = routing.Match(context.Request.Path);
    }

    if (route is null)
    {
        await ResultHttpMapper.Problem(Error.NotFound("gateway.route", "errors:notFound").WithMetadata("path", context.Request.Path.Value))
            .ExecuteAsync(context);
        return;
    }

    await proxy.ForwardAsync(context, route.Service);
}).AllowAnonymous();

app.Run();
