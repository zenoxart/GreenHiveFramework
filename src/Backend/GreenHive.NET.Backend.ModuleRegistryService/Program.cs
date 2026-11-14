using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.ModuleRegistryService;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("module-registry");
builder.Services.AddSingleton<ModuleRegistry>();
builder.Services.AddScoped<CompatibilityEvaluator>();
builder.Services.AddScoped<PublishModule>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/modules");
api.MapGet("/catalog", (string? hostVersion, string? desktopRuntime, CompatibilityEvaluator evaluator, CancellationToken ct) =>
    evaluator.CatalogAsync(hostVersion, desktopRuntime, ct).ToHttp());
api.MapGet("/", (ModuleRegistry registry, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "modules.view").BindAsync(_ => registry.PublishedAsync(ct))
        .Map(list => list.Select(m => new ModuleVersionInfo(m.Manifest.ModuleId, m.Manifest.Version, m.Channel, m.Status, m.PublishedAt) { PublishedBy = m.PublishedBy }))
        .ToHttp());
api.MapGet("/{moduleId}/versions", (string moduleId, ModuleRegistry registry, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "modules.view").BindAsync(_ => registry.VersionsAsync(moduleId, ct))
        .Map(list => list.Select(m => new ModuleVersionInfo(m.Manifest.ModuleId, m.Manifest.Version, m.Channel, m.Status, m.PublishedAt) { PublishedBy = m.PublishedBy }))
        .ToHttp());
api.MapGet("/{moduleId}/{version}", (string moduleId, string version, ModuleRegistry registry, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "modules.view").BindAsync(_ => registry.FindAsync(moduleId, version, ct))
        .Bind(m => m.ToResult(Steps.NotFound("modules.version", $"{moduleId}@{version}"))).Map(m => m.Manifest).ToHttp());
api.MapPost("/", (PublishModuleRequest request, PublishModule useCase, CancellationToken ct) => useCase.ExecuteAsync(request, ct).ToHttp());
api.MapPost("/{moduleId}/{version}/withdraw", (string moduleId, string version, ModuleRegistry registry, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "modules.publish").BindAsync(_ => registry.SetStatusAsync(moduleId, version, "withdrawn", ct))
        .Bind(rows => rows > 0 ? Result.Success() : Result.Failure(Steps.NotFound("modules.version", $"{moduleId}@{version}"))).ToHttp());

var internalApi = app.MapInternal();
internalApi.MapGet("/routes", (ModuleRegistry registry, CancellationToken ct) =>
    registry.LatestAsync(ct).Map(list => list.Where(m => m.Manifest.Backend is not null)
        .Select(m => new RouteInfo(m.Manifest.Backend!.Route, m.Manifest.Backend.Service))).ToHttp());
internalApi.MapGet("/modules", (ModuleRegistry registry, CancellationToken ct) => registry.LatestAsync(ct).Map(list => list.Select(m => m.Manifest)).ToHttp());

app.Run();
