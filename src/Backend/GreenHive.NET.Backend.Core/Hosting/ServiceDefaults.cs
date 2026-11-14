using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenHive.NET.Backend.Core.Hosting;

/// <summary>
/// The standard of every service: configuration via environment variables, JWT authentication, JSON conventions,
/// correlation ID, exception boundary, health checks (/health/live, /health/ready), internal endpoints and the outbox.
/// </summary>
public static class ServiceDefaults
{
    public static WebApplicationBuilder AddGreenHiveDefaults(this WebApplicationBuilder builder, string serviceName, bool useDatabase = true)
    {
        var services = builder.Services;
        services.Configure<GreenHiveOptions>(builder.Configuration.GetSection(GreenHiveOptions.Section));
        services.PostConfigure<GreenHiveOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.ServiceName) || o.ServiceName == "service")
            {
                o.ServiceName = serviceName;
            }
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        });
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

        services.ConfigureHttpJsonOptions(o => GreenHiveJson.Configure(o.SerializerOptions));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TokenService>();
        services.AddHttpContextAccessor();
        services.AddHttpClient("greenhive", c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<ServiceClient>();
        services.AddScoped<RequestContext>();
        services.AddSingleton<OutboxWriter>();
        services.AddAuthentication(JwtAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, JwtAuthenticationHandler>(JwtAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();

        Try.AddMapper(ex => ex is BadHttpRequestException bad
            ? Error.Validation("request.invalid", bad.Message)
            : null);

        if (useDatabase)
        {
            var connectionString = builder.Configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException("Connection string 'Database' is missing (ConnectionStrings__Database).");
            services.AddSingleton(_ => PostgresDataSource.Create(connectionString));
            services.AddSingleton<Db>();
            services.AddSingleton<AuditLog>();
            services.AddHostedService<OutboxDispatcher>();
        }

        return builder;
    }

    public static WebApplication UseGreenHiveDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionBoundaryMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        var options = app.Services.GetRequiredService<IOptions<GreenHiveOptions>>().Value;
        app.MapGet("/health/live", () => Results.Ok(new { status = "live", service = options.ServiceName })).AllowAnonymous();
        app.MapGet("/health/ready", async (IServiceProvider sp, CancellationToken ct) =>
        {
            var db = sp.GetService<Db>();
            var ready = db is null || await db.PingAsync(ct);
            return ready
                ? Results.Ok(new { status = "ready", service = options.ServiceName })
                : Results.Json(new { status = "database unavailable", service = options.ServiceName }, statusCode: 503);
        }).AllowAnonymous();

        app.Logger.LogInformation("{Service} started", options.ServiceName);
        return app;
    }

    /// <summary>Endpoints for service-to-service calls; they require the internal key and are never routed by the gateway.</summary>
    public static RouteGroupBuilder MapInternal(this IEndpointRouteBuilder endpoints, string prefix = "/internal") =>
        endpoints.MapGroup(prefix).AllowAnonymous().AddEndpointFilter(async (context, next) =>
        {
            var expected = context.HttpContext.RequestServices.GetRequiredService<IOptions<GreenHiveOptions>>().Value.InternalKey;
            var provided = context.HttpContext.Request.Headers[ServiceClient.InternalKeyHeader].ToString();
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(provided), System.Text.Encoding.UTF8.GetBytes(expected)))
            {
                return ResultHttpMapper.Problem(Error.Forbidden("internal.key", "auth:internalOnly"));
            }

            return await next(context);
        });

    /// <summary>A route group that requires an authenticated caller.</summary>
    public static RouteGroupBuilder MapApi(this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup(prefix).RequireAuthorization();
}
