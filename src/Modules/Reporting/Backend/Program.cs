using System.Text.Json.Nodes;
using GreenHive.NET.Modules.Reporting.Backend;
using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Monads;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("reporting-module");
builder.Services.AddSingleton<ReportEngine>();
builder.Services.AddScoped<RunReport>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/reports");
api.MapGet("/", (ReportEngine engine, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, ReportPermissions.View).BindAsync(_ => engine.DefinitionsAsync(ct)).ToHttp());
api.MapGet("/runs", (RunReport useCase, CancellationToken ct) => useCase.RunsAsync(ct).ToHttp());
api.MapGet("/runs/{runId:guid}", (Guid runId, RunReport useCase, CancellationToken ct) => useCase.FindRunAsync(runId, ct).ToHttp());
api.MapGet("/{id}", (string id, ReportEngine engine, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, ReportPermissions.View).BindAsync(_ => engine.FindAsync(id, ct)).ToHttp());
api.MapPost("/{id}/run", (string id, RunReportRequest request, RunReport useCase, CancellationToken ct) =>
    useCase.ExecuteAsync(id, request.Parameters, ct).ToHttp());

// Headless workflow step reports.runReport: inputs { reportId, parameters (object or JSON text) }.
api.MapPost("/workflow-steps/{stepType}", (string stepType, JsonObject inputs, RunReport useCase, CancellationToken ct) =>
    (stepType == "reports.runReport"
        ? useCase.ExecuteAsync(inputs["reportId"]?.ToString() ?? string.Empty,
                inputs["parameters"] switch
                {
                    JsonObject o => o,
                    JsonValue v when v.ToString().TrimStart().StartsWith('{') => JsonNode.Parse(v.ToString()) as JsonObject,
                    _ => null,
                }, ct)
            .Map(r => new JsonObject { ["runId"] = r.RunId.ToString(), ["rowCount"] = r.Rows.Count, ["total"] = r.Total })
        : Task.FromResult(Result<JsonObject>.Failure(Error.NotFound("reports.stepType.unknown", "workflow:stepType.unknown"))))
    .ToHttp());

app.Run();
