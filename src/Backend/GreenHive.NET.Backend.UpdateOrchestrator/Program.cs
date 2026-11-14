using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.UpdateOrchestrator;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("update-orchestrator");
builder.Services.AddSingleton<RolloutRepository>();
builder.Services.AddScoped<StartRollout>();
builder.Services.AddScoped<ChangeRolloutStage>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/updates");
api.MapGet("/rollouts", (RolloutRepository rollouts, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "updates.manage").BindAsync(_ => rollouts.ListAsync(ct)).ToHttp());
api.MapGet("/rollouts/{id:guid}", (Guid id, RolloutRepository rollouts, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "updates.manage").BindAsync(_ => rollouts.FindAsync(id, ct)).ToHttp());
api.MapPost("/rollouts/{id:guid}/advance", (Guid id, bool? force, ChangeRolloutStage useCase, CancellationToken ct) =>
    useCase.ExecuteAsync(id, r => RolloutPlanner.Advance(r, force == true), "advanced", null, ct).ToHttp());
api.MapPost("/rollouts/{id:guid}/hold", (Guid id, string? reason, ChangeRolloutStage useCase, CancellationToken ct) =>
    useCase.ExecuteAsync(id, RolloutPlanner.Hold, "held", reason, ct).ToHttp());
api.MapPost("/rollouts/{id:guid}/cancel", (Guid id, ChangeRolloutStage useCase, CancellationToken ct) =>
    useCase.ExecuteAsync(id, RolloutPlanner.Cancel, "cancelled", null, ct).ToHttp());

// The module versions assigned to the signed-in user (used by the client to hot-swap modules).
api.MapGet("/assignments", (RolloutRepository rollouts, RequestContext context, CancellationToken ct) =>
    rollouts.LatestPerModuleAsync(ct).Map(list => list.Select(r => new ModuleAssignment(r.ModuleId, RolloutPlanner.VersionFor(r, context.User.Id), r.Stage))).ToHttp());

var internalApi = app.MapInternal();
internalApi.MapPost("/rollouts", (CreateRolloutRequest request, StartRollout useCase, CancellationToken ct) => useCase.ExecuteAsync(request, ct).ToHttp());
internalApi.MapGet("/assignments", (Guid userId, RolloutRepository rollouts, CancellationToken ct) =>
    rollouts.LatestPerModuleAsync(ct).Map(list => list.Select(r => new ModuleAssignment(r.ModuleId, RolloutPlanner.VersionFor(r, userId), r.Stage))).ToHttp());

app.Run();
