// Shared by GreenHive.NET.Modules.Reporting.Backend and .Client (compiled into both - one wire format).
using System.Text.Json.Nodes;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Modules.Reporting.Contracts;

public static class ReportPermissions
{
    public const string View = "reports.view";
    public const string Run = "reports.run";
}

/// <summary>A report of the catalogue; its parameters use the workflow InputField model (same form generator).</summary>
public sealed record ReportDefinitionDto(string Id, string Name, string Description, string Category, string Chart)
{
    public IReadOnlyList<InputField> Parameters { get; init; } = [];

    public string Permission { get; init; } = ReportPermissions.Run;
}

public sealed record RunReportRequest(JsonObject? Parameters);

public enum ReportColumnKind
{
    Text,
    Number,
    Currency,
    Date,
}

public sealed record ReportColumn(string Name, string Label, ReportColumnKind Kind = ReportColumnKind.Text);

public sealed record ChartPoint(string Label, decimal Value);

/// <summary>Result of a report run: table (values formatted invariantly) plus one chart series.</summary>
public sealed record ReportResult(Guid RunId, string ReportId, string Name, string Chart)
{
    public IReadOnlyList<ReportColumn> Columns { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = [];

    public IReadOnlyList<ChartPoint> Series { get; init; } = [];

    public string SeriesLabel { get; init; } = string.Empty;

    public decimal? Total { get; init; }

    public JsonObject Parameters { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record ReportRunDto(Guid Id, string ReportId, string ReportName, string Status, string RequestedByName, DateTimeOffset CreatedAt)
{
    public DateTimeOffset? FinishedAt { get; init; }

    public int RowCount { get; init; }
}
