using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Modules.Reporting.Client.ViewModels;

/// <summary>Report catalogue, the selected report's parameter schema and the last result.</summary>
public sealed class ReportCatalogViewModel : ModuleViewModel
{
    private IReadOnlyList<ReportDefinitionDto> _reports = [];
    private ReportDefinitionDto? _selected;
    private ReportResult? _result;
    private string? _pendingId;

    public IReadOnlyList<ReportDefinitionDto> Reports
    {
        get => _reports;
        private set => SetProperty(ref _reports, value);
    }

    public ReportDefinitionDto? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                Result = null;
            }
        }
    }

    public ReportResult? Result
    {
        get => _result;
        private set => SetProperty(ref _result, value);
    }

    public bool CanRun => Selected is { } r && Has(r.Permission);

    protected override void OnCreated() => Title = T("view.catalog.label");

    public override void OnNavigatedTo(object? parameter)
    {
        _pendingId = parameter as string;
        _ = LoadAsync();
    }

    public Task<bool> LoadAsync() =>
        RunAsync(ct => Api.GetAsync<IReadOnlyList<ReportDefinitionDto>>(string.Empty, ct), list =>
        {
            Reports = list;
            var id = _pendingId ?? Selected?.Id;
            _pendingId = null;
            Selected = list.FirstOrDefault(r => r.Id == id) ?? Selected ?? list.FirstOrDefault();
        });

    /// <summary>The report's parameters as an input schema for the shared InputForm.</summary>
    public InputSchema Schema(ReportDefinitionDto report) => new($"reports.{report.Id}") { Fields = report.Parameters };

    /// <summary>Default values ("default" of each field) for the form.</summary>
    public static JsonObject Defaults(ReportDefinitionDto report)
    {
        var values = new JsonObject();
        foreach (var field in report.Parameters.Where(f => f.Default is not null))
        {
            values[field.Name] = JsonValue.Create(field.Default);
        }

        if (report.Parameters.Any(f => f.Name == "to") && !values.ContainsKey("to"))
        {
            values["to"] = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return values;
    }

    public Task<bool> ExecuteAsync(JsonObject parameters) =>
        Selected is not { } report
            ? Task.FromResult(false)
            : RunAsync(ct => Api.PostAsync<ReportResult>($"{Uri.EscapeDataString(report.Id)}/run", new RunReportRequest(parameters), ct), r => Result = r);

    /// <summary>Formats a cell (values arrive invariantly formatted) for display in the current culture.</summary>
    public static string Display(ReportColumn column, string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return column.Kind switch
        {
            ReportColumnKind.Number when decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) => n.ToString("N0", CultureInfo.CurrentCulture),
            ReportColumnKind.Currency when decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var c) => c.ToString("N2", CultureInfo.CurrentCulture),
            ReportColumnKind.Date when DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var d) => d.ToString("d", CultureInfo.CurrentCulture),
            _ => value,
        };
    }

    public string ToCsv()
    {
        if (Result is not { } r)
        {
            return string.Empty;
        }

        static string Escape(string? v) => v is null ? string.Empty : v.Contains(';') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(";", r.Columns.Select(c => Escape(c.Label))));
        foreach (var row in r.Rows)
        {
            csv.AppendLine(string.Join(";", row.Select(Escape)));
        }

        return csv.ToString();
    }

    public void Copied() => InfoText = T("copied");
}
