using GreenHive.NET.Backend.AiAgentService;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("ai-agent-service");
if (string.Equals(builder.Configuration["GreenHive:Ai:Provider"], "openai", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();
}
else
{
    builder.Services.AddSingleton<IAiProvider, RulesAiProvider>();
}

builder.Services.AddSingleton<AiAuditLog>();
builder.Services.AddSingleton<AiGuardrails>();
builder.Services.AddSingleton<AiToolCatalog>();
builder.Services.AddSingleton<AiWorkflowAgent>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AiWorkflowAgent>());

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/ai");
api.MapGet("/status", async (IAiProvider provider, AiAuditLog audit, Db db, RequestContext context, CancellationToken ct) =>
    await Steps.Authenticated(context.User)
        .BindAsync(user => audit.TodayAsync(user.TenantId, ct))
        .BindAsync(async today =>
        {
            var enabled = await db.ScalarAsync<bool?>("select enabled from ai.switches where scope = 'global'", null, ct);
            return Result.Success(new AiStatus(provider.Name, provider.Model, enabled.GetValueOrDefault(true) ?? true)
            {
                RunsToday = today.Runs,
                TokensToday = today.Tokens,
            });
        })
        .ToHttp());
api.MapGet("/audit", (Guid? instanceId, AiAuditLog audit, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "workflows.view")
        .BindAsync(_ => instanceId is { } id ? audit.ForInstanceAsync(id, ct) : audit.RecentAsync(context.User.TenantId, ct))
        .ToHttp());
api.MapPost("/switch", (AiSwitchRequest request, Db db, AuditLog auditLog, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "ai.admin")
        .BindAsync(_ => db.ExecuteAsync(
            """
            insert into ai.switches (scope, enabled, changed_at, changed_by) values ('global', @enabled, now(), @by)
            on conflict (scope) do update set enabled = excluded.enabled, changed_at = now(), changed_by = excluded.changed_by
            """, new { enabled = request.Enabled, by = context.User.UserName }, ct))
        .TapAsync(_ => auditLog.WriteAsync(context.User, request.Enabled ? "ai.enabled" : "ai.disabled", "ai", "global", ct: ct))
        .Map(_ => request)
        .ToHttp());

app.MapInternal().MapPost("/runs", (AiRunRequest request, AiWorkflowAgent agent) =>
{
    agent.Enqueue(request);
    return Results.Accepted();
});

app.Run();
