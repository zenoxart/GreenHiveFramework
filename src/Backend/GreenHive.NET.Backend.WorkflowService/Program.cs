using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Backend.WorkflowService;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("workflow-service");
builder.Services.AddSingleton<WorkflowRepository>();
builder.Services.AddSingleton<DefinitionCache>();
builder.Services.AddSingleton<ModuleDirectory>();
builder.Services.AddSingleton<TokenBroker>();
builder.Services.AddSingleton<StepExecutor>();
builder.Services.AddSingleton<AiDispatcher>();
builder.Services.AddSingleton<WorkflowEngine>();
builder.Services.AddSingleton<TriggerEngine>();
builder.Services.AddSingleton<ImpactAnalyzer>();
builder.Services.AddHostedService<WorkflowScheduler>();
builder.Services.AddScoped<SaveWorkflow>();
builder.Services.AddScoped<StartWorkflow>();
builder.Services.AddScoped<GetInbox>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/workflows");

// ---- definitions
api.MapGet("/", async (WorkflowRepository repository, RequestContext context, CancellationToken ct) =>
    await Steps.Authorize(context.User, "workflows.view")
        .BindAsync(_ => repository.LatestAsync(context.User.TenantId, ct))
        .BindAsync(async list =>
        {
            var summaries = new List<WorkflowSummary>();
            foreach (var d in list.Where(d => d.Status != WorkflowStatus.Archived).OrderBy(d => d.Name))
            {
                summaries.Add(WorkflowRepository.Summarize(d, (await repository.CountRunningAsync(d.Id, ct)).GetValueOrDefault(0)));
            }

            return Result.Success<IReadOnlyList<WorkflowSummary>>(summaries);
        })
        .ToHttp());
api.MapGet("/{id:guid}", (Guid id, WorkflowRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "workflows.view").BindAsync(_ => repository.FindLatestAsync(id, ct)).ToHttp());
api.MapGet("/{id:guid}/versions", (Guid id, WorkflowRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "workflows.view").BindAsync(_ => repository.VersionsAsync(id, ct)).ToHttp());
api.MapPost("/", (SaveWorkflowRequest request, SaveWorkflow useCase, CancellationToken ct) => useCase.ExecuteAsync(request, ct).ToHttp());
api.MapPost("/{id:guid}/status/{status}", async (Guid id, WorkflowStatus status, WorkflowRepository repository, TriggerEngine triggers,
    GreenHive.NET.Backend.Core.Data.Db db, RequestContext context, CancellationToken ct) =>
    await Steps.Authorize(context.User, "workflows.edit")
        .BindAsync(_ => repository.FindLatestAsync(id, ct))
        .BindAsync(d => db.InTransactionAsync(session => repository.UpdateDefinitionAsync(session, d with { Status = status }, ct), ct: ct).Map(_ => d with { Status = status }))
        .Tap(_ => triggers.Invalidate())
        .ToHttp());
api.MapPost("/{id:guid}/start", (Guid id, StartWorkflowRequest request, StartWorkflow useCase, CancellationToken ct) => useCase.ExecuteAsync(id, request, ct).ToHttp());

// ---- inbox and instances
api.MapGet("/inbox", (GetInbox useCase, CancellationToken ct) => useCase.ExecuteAsync(ct).ToHttp());
api.MapGet("/instances", (string? state, Guid? workflowId, bool? mine, WorkflowRepository repository, RequestContext context, CancellationToken ct) =>
    (mine == true ? Steps.Authenticated(context.User).ToUnit() : Steps.Authorize(context.User, "workflows.view"))
        .BindAsync(_ => repository.InstancesAsync(context.User.TenantId, state, workflowId, mine == true ? context.User.Id : null, 200, ct))
        .Map(list => list.Select(i => new InstanceSummary(i.Id, i.WorkflowId, i.WorkflowName, i.State)
        {
            CurrentStepId = i.CurrentStepId,
            Executor = i.Executor,
            RunAsName = i.RunAsName ?? string.Empty,
            TriggeredBy = i.TriggeredBy,
            LastError = i.LastError,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
        }))
        .ToHttp());
api.MapGet("/instances/{id:guid}", (Guid id, WorkflowRepository repository, DefinitionCache definitions, RequestContext context, CancellationToken ct) =>
    repository.FindInstanceAsync(id, ct)
        .Ensure(i => i.RunAs == context.User.Id || context.User.Has("workflows.view"), Error.Forbidden("workflow.instance.forbidden", "auth:permissionMissing"))
        .BindAsync(i => definitions.GetAsync(i.WorkflowId, i.WorkflowVersion, ct).Map(d => new InstanceDetails(
            i with { Variables = InputValidator.Mask(i.Variables, d.IsSensitive) }, d)))
        .ToHttp());
api.MapPost("/instances/{id:guid}/complete", (Guid id, CompleteStepRequest request, WorkflowEngine engine, RequestContext context, CancellationToken ct) =>
    engine.CompleteAsync(id, request, new StepActor(context.User, ActorKind.Human), ct).ToHttp());
api.MapPost("/instances/{id:guid}/handover", (Guid id, HandOverRequest request, WorkflowEngine engine, RequestContext context, CancellationToken ct) =>
    (request.Executor == WorkflowExecutor.Ai ? Steps.Authorize(context.User, "ai.run") : Steps.Authenticated(context.User).ToUnit())
        .BindAsync(_ => engine.HandOverAsync(id, request, context.User, ct)).ToHttp());
api.MapPost("/instances/{id:guid}/cancel", (Guid id, WorkflowEngine engine, RequestContext context, CancellationToken ct) =>
    engine.CancelAsync(id, context.User, ct).ToHttp());
api.MapPost("/instances/{id:guid}/resume", (Guid id, WorkflowEngine engine, RequestContext context, CancellationToken ct) =>
    engine.ResumeAsync(id, context.User, ct).ToHttp());

// ---- impact analysis
api.MapGet("/impact", (WorkflowRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "workflows.view").BindAsync(_ => repository.ReportsAsync(ct)).ToHttp());
api.MapGet("/impact/{id:guid}", (Guid id, WorkflowRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "workflows.view").BindAsync(_ => repository.FindReportAsync(id, ct)).ToHttp());

// ---- command triggers (reported by the client after a command ran)
api.MapPost("/triggers/command/{commandId}", (string commandId, System.Text.Json.Nodes.JsonObject? parameters, TriggerEngine triggers,
    RequestContext context, CancellationToken ct) =>
    triggers.OnCommandAsync(commandId, parameters, context.User, ct).Map(started => new { started }).ToHttp());

// ---- internal (NotificationPoint, Module Registry, AiAgentService)
var internalApi = app.MapInternal();
internalApi.MapPost("/events", async (NotificationEnvelope[] envelopes, TriggerEngine triggers, CancellationToken ct) =>
{
    await triggers.OnEventsAsync(envelopes, ct);
    return Results.Ok(new { received = envelopes.Length });
});
internalApi.MapPost("/impact", (ImpactAnalysisRequest request, ImpactAnalyzer analyzer, TriggerEngine triggers, CancellationToken ct) =>
    analyzer.AnalyzeAsync(request, ct).Tap(_ => triggers.Invalidate()).ToHttp());
internalApi.MapGet("/instances/{id:guid}", (Guid id, WorkflowRepository repository, DefinitionCache definitions, CancellationToken ct) =>
    repository.FindInstanceAsync(id, ct).BindAsync(i => definitions.GetAsync(i.WorkflowId, i.WorkflowVersion, ct).Map(d => new InstanceDetails(i, d))).ToHttp());
internalApi.MapPost("/instances/{id:guid}/proposal", (Guid id, AiProposalRequest request, WorkflowEngine engine, CancellationToken ct) =>
    engine.ProposeAsync(id, request, ct).ToHttp());
internalApi.MapPost("/instances/{id:guid}/complete", (Guid id, AiCompleteRequest request, WorkflowEngine engine, CancellationToken ct) =>
    engine.CompleteAsync(id, new CompleteStepRequest(request.StepId) { Outputs = request.Outputs, Decision = request.Decision },
        new StepActor(GreenHiveUser.Anonymous with { UserName = "ai-agent", DisplayName = "AI agent" }, ActorKind.Ai)
        {
            Reasoning = request.Reasoning,
            Confidence = request.Confidence,
        }, ct).ToHttp());
internalApi.MapPost("/instances/{id:guid}/handover", (Guid id, HandOverRequest request, WorkflowEngine engine, CancellationToken ct) =>
    engine.HandOverAsync(id, request, null, ct).ToHttp());
internalApi.MapPost("/instances/{id:guid}/tools/{stepTypeId}", (Guid id, string stepTypeId, System.Text.Json.Nodes.JsonObject inputs, WorkflowEngine engine,
    CancellationToken ct) => engine.ExecuteToolAsync(id, stepTypeId, inputs, ct).ToHttp());

app.Run();
