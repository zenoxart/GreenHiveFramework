using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Modules.Reporting.Backend;

/// <summary>Run a report: load definition -> authorize -> validate parameters (InputValidator) -> execute -> store run + reports.ready -> project.</summary>
public sealed class RunReport(ReportEngine engine, Db db, OutboxWriter outbox, RequestContext context)
{
    public Task<Result<ReportResult>> ExecuteAsync(string reportId, JsonObject? parameters, CancellationToken ct) =>
        Steps.Authorize(context.User, ReportPermissions.View)
            .BindAsync(_ => engine.FindAsync(reportId, ct))
            .Bind(definition => Steps.Authorize(context.User, definition.Permission).Map(_ => definition))
            .Bind(definition => InputValidator.Validate(new InputSchema($"reports.{definition.Id}") { Fields = definition.Parameters }, parameters).ToResult()
                .Map(valid => (Definition: definition, Parameters: valid)))
            .BindAsync(x => engine.ExecuteAsync(x.Definition.Id, x.Parameters, ct).Map(data => new ReportResult(Guid.NewGuid(), x.Definition.Id, x.Definition.Name, x.Definition.Chart)
            {
                Columns = data.Columns,
                Rows = data.Rows,
                Series = data.Series,
                SeriesLabel = data.SeriesLabel,
                Total = data.Total,
                Parameters = x.Parameters,
                CreatedAt = context.Now,
            }))
            .TapAsync(result => db.InTransactionAsync(session =>
                session.ExecuteAsync(
                        """
                        insert into reporting.report_runs (id, report_id, parameters, requested_by, requested_by_name, status, result, created_at, finished_at)
                        values (@id, @reportId, @parameters::jsonb, @userId, @userName, 'completed', @result::jsonb, @now, @now)
                        """,
                        new
                        {
                            id = result.RunId,
                            reportId = result.ReportId,
                            parameters = result.Parameters.ToJsonString(),
                            userId = context.User.Id,
                            userName = context.User.AuditName,
                            result = JsonSerializer.Serialize(result, GreenHiveJson.Options),
                            now = context.Now,
                        }, ct)
                    .BindAsync(_ => outbox.AddAsync(session, outbox.Create(EventTypes.ReportReady, new JsonObject
                    {
                        ["runId"] = result.RunId.ToString(),
                        ["reportId"] = result.ReportId,
                        ["rowCount"] = result.Rows.Count,
                    }, NotificationAudience.ForUser(context.User.Id)) with
                    {
                        Title = "reports:notification.ready",
                        Message = result.Name,
                        Severity = NotificationSeverity.Success,
                        NavigationTarget = "reports.runs",
                        NavigationParameter = result.RunId.ToString(),
                    }, ct)), ct: ct));

    public Task<Result<IReadOnlyList<ReportRunDto>>> RunsAsync(CancellationToken ct) =>
        Steps.Authorize(context.User, ReportPermissions.View).BindAsync(_ => db.QueryAsync(
            """
            select r.id, r.report_id, d.name, r.status, r.requested_by_name, r.created_at, r.finished_at,
                   coalesce(jsonb_array_length(r.result -> 'rows'), 0) as row_count
            from reporting.report_runs r join reporting.report_definitions d on d.id = r.report_id
            where r.requested_by = @userId order by r.created_at desc limit 50
            """,
            r => new ReportRunDto(r.Get<Guid>("id"), r.Get<string>("report_id"), r.Get<string>("name"), r.Get<string>("status"), r.Get<string>("requested_by_name"),
                r.GetTime("created_at")) { FinishedAt = r.GetTimeOrNull("finished_at"), RowCount = r.Get<int>("row_count") },
            new { userId = context.User.Id }, ct));

    public Task<Result<ReportResult>> FindRunAsync(Guid runId, CancellationToken ct) =>
        Steps.Authorize(context.User, ReportPermissions.View).BindAsync(_ => db.FindAsync(
            "select result::text as result from reporting.report_runs where id = @runId and (requested_by = @userId or @admin)",
            r => JsonSerializer.Deserialize<ReportResult>(r.Get<string>("result"), GreenHiveJson.Options)!,
            new { runId, userId = context.User.Id, admin = context.User.Has("*") },
            Error.NotFound("reports.run.notFound", "reports:run.notFound"), ct));
}
