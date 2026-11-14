using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.ModuleContracts.Api;

namespace GreenHive.NET.Backend.ApiGateway;

/// <summary>
/// The route table: static routes (GreenHive__Routes__/api/identity=http://identity-service:8080) plus the module
/// backend routes of the latest published module versions (manifest.backend.route / service).
/// </summary>
public sealed class RoutingConfiguration(IConfiguration configuration, ServiceClient services, ILogger<RoutingConfiguration> logger) : BackgroundService
{
    private volatile IReadOnlyList<RouteInfo> _routes = [];

    public IReadOnlyList<RouteInfo> Routes => _routes;

    /// <summary>Longest matching prefix wins.</summary>
    public RouteInfo? Match(PathString path) =>
        _routes.Where(r => path.StartsWithSegments(r.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Prefix.Length)
            .FirstOrDefault();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        var routes = new Dictionary<string, RouteInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (prefix, service) in configuration.GetSection("GreenHive:Routes").Get<Dictionary<string, string>>() ?? [])
        {
            routes[prefix] = new RouteInfo(prefix, service);
        }

        var dynamic = await services.GetAsync<IReadOnlyList<RouteInfo>>("module-registry", "/internal/routes", null, ct);
        if (dynamic.TryGetValue(out var moduleRoutes, out var error))
        {
            foreach (var route in moduleRoutes)
            {
                // Static configuration wins (e.g. to point a module at a local development instance).
                routes.TryAdd(route.Prefix, route);
            }
        }
        else
        {
            logger.LogWarning("Module routes could not be loaded from the registry: {Error}", error);
        }

        _routes = routes.Values.ToList();
    }
}
