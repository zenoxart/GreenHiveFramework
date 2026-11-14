using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Modules.Reporting.Backend;

/// <summary>Report definitions and the queries behind them (adapter). Every report returns columns, rows and one chart series.</summary>
public sealed class ReportEngine(Db db)
{
    public Task<Result<IReadOnlyList<ReportDefinitionDto>>> DefinitionsAsync(CancellationToken ct) =>
        db.QueryAsync("select id, name, description, category, chart, parameters::text as parameters, permission from reporting.report_definitions order by category, name",
            Map, null, ct);

    public Task<Result<ReportDefinitionDto>> FindAsync(string id, CancellationToken ct) =>
        db.FindAsync("select id, name, description, category, chart, parameters::text as parameters, permission from reporting.report_definitions where id = @id",
            Map, new { id }, Error.NotFound("reports.report.notFound", "reports:report.notFound").WithMetadata("id", id), ct);

    /// <summary>Runs the query of a report with validated parameters.</summary>
    public async Task<Result<(IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows, IReadOnlyList<ChartPoint> Series, string SeriesLabel, decimal? Total)>>
        ExecuteAsync(string reportId, JsonObject p, CancellationToken ct)
    {
        DateOnly Date(string name, DateOnly fallback) =>
            DateOnly.TryParse(p[name]?.ToString(), CultureInfo.InvariantCulture, out var d) ? d : fallback;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        switch (reportId)
        {
            case "sales-by-region":
            {
                var rows = await db.QueryAsync(
                    """
                    select region, sum(quantity)::int as units, sum(revenue) as revenue from reporting.sales
                    where sold_on between @from and @to group by region order by revenue desc
                    """,
                    r => (Region: r.Get<string>("region"), Units: r.Get<int>("units"), Revenue: r.Get<decimal>("revenue")),
                    new { from = Date("from", new DateOnly(today.Year, 1, 1)), to = Date("to", today) }, ct);
                return rows.Map(list => Shape(
                    [new("region", "Region"), new("units", "Units", ReportColumnKind.Number), new("revenue", "Revenue", ReportColumnKind.Currency)],
                    list.Select(x => Row(x.Region, x.Units, x.Revenue)),
                    list.Select(x => new ChartPoint(x.Region, x.Revenue)), "Revenue", list.Sum(x => x.Revenue)));
            }

            case "sales-by-month":
            {
                var year = int.TryParse(p["year"]?.ToString(), out var y) ? y : today.Year;
                var rows = await db.QueryAsync(
                    """
                    select extract(month from sold_on)::int as month, sum(quantity)::int as units, sum(revenue) as revenue from reporting.sales
                    where extract(year from sold_on)::int = @year group by 1 order by 1
                    """,
                    r => (Month: r.Get<int>("month"), Units: r.Get<int>("units"), Revenue: r.Get<decimal>("revenue")), new { year }, ct);
                return rows.Map(list => Shape(
                    [new("month", "Month"), new("units", "Units", ReportColumnKind.Number), new("revenue", "Revenue", ReportColumnKind.Currency)],
                    list.Select(x => Row(CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(x.Month), x.Units, x.Revenue)),
                    list.Select(x => new ChartPoint(CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(x.Month), x.Revenue)),
                    $"Revenue {year}", list.Sum(x => x.Revenue)));
            }

            case "top-products":
            {
                var top = int.TryParse(p["top"]?.ToString(), out var t) ? Math.Clamp(t, 1, 20) : 5;
                var rows = await db.QueryAsync(
                    """
                    select product, sum(quantity)::int as units, sum(revenue) as revenue from reporting.sales
                    where sold_on between @from and @to group by product order by revenue desc limit @top
                    """,
                    r => (Product: r.Get<string>("product"), Units: r.Get<int>("units"), Revenue: r.Get<decimal>("revenue")),
                    new { from = Date("from", new DateOnly(today.Year, 1, 1)), to = Date("to", today), top }, ct);
                return rows.Map(list => Shape(
                    [new("product", "Product"), new("units", "Units", ReportColumnKind.Number), new("revenue", "Revenue", ReportColumnKind.Currency)],
                    list.Select(x => Row(x.Product, x.Units, x.Revenue)),
                    list.Select(x => new ChartPoint(x.Product, x.Revenue)), "Revenue", list.Sum(x => x.Revenue)));
            }

            case "invoice-aging":
            {
                var rows = await db.QueryAsync(
                    """
                    select bucket, count(*)::int as invoices, sum(amount) as amount, min(ord) as ord from (
                      select amount, case
                        when due_on < current_date - 30 then 'Overdue > 30 days'
                        when due_on < current_date then 'Overdue 1-30 days'
                        when due_on <= current_date + 7 then 'Due within 7 days'
                        when due_on <= current_date + 30 then 'Due within 30 days'
                        else 'Due later' end as bucket,
                      case
                        when due_on < current_date - 30 then 1 when due_on < current_date then 2
                        when due_on <= current_date + 7 then 3 when due_on <= current_date + 30 then 4 else 5 end as ord
                      from documents.invoices where status in ('submitted', 'approved')) x
                    group by bucket order by ord
                    """,
                    r => (Bucket: r.Get<string>("bucket"), Count: r.Get<int>("invoices"), Amount: r.Get<decimal>("amount")), null, ct);
                return rows.Map(list => Shape(
                    [new("bucket", "Due"), new("invoices", "Invoices", ReportColumnKind.Number), new("amount", "Amount", ReportColumnKind.Currency)],
                    list.Select(x => Row(x.Bucket, x.Count, x.Amount)),
                    list.Select(x => new ChartPoint(x.Bucket, x.Amount)), "Open amount", list.Sum(x => x.Amount)));
            }

            case "approval-throughput":
            {
                var rows = await db.QueryAsync(
                    """
                    select coalesce(decided_by_name, '(undecided)') as approver,
                           count(*) filter (where status in ('approved', 'paid'))::int as approved,
                           count(*) filter (where status = 'rejected')::int as rejected,
                           coalesce(sum(amount) filter (where status in ('approved', 'paid')), 0) as amount
                    from documents.invoices where decided_by_name is not null group by 1 order by approved desc
                    """,
                    r => (Approver: r.Get<string>("approver"), Approved: r.Get<int>("approved"), Rejected: r.Get<int>("rejected"), Amount: r.Get<decimal>("amount")),
                    null, ct);
                return rows.Map(list => Shape(
                    [new("approver", "Approver"), new("approved", "Approved", ReportColumnKind.Number), new("rejected", "Rejected", ReportColumnKind.Number),
                     new("amount", "Approved amount", ReportColumnKind.Currency)],
                    list.Select(x => Row(x.Approver, x.Approved, x.Rejected, x.Amount)),
                    list.Select(x => new ChartPoint(x.Approver, x.Approved)), "Approved invoices", null));
            }

            default:
                return Error.NotFound("reports.report.notFound", "reports:report.notFound").WithMetadata("id", reportId);
        }
    }

    private static (IReadOnlyList<ReportColumn>, IReadOnlyList<IReadOnlyList<string?>>, IReadOnlyList<ChartPoint>, string, decimal?) Shape(
        IReadOnlyList<ReportColumn> columns, IEnumerable<IReadOnlyList<string?>> rows, IEnumerable<ChartPoint> series, string label, decimal? total) =>
        (columns, rows.ToList(), series.ToList(), label, total);

    private static IReadOnlyList<string?> Row(params object?[] values) =>
        values.Select(v => v switch
        {
            null => null,
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString(),
        }).ToList();

    private static ReportDefinitionDto Map(System.Data.Common.DbDataReader r) =>
        new(r.Get<string>("id"), r.Get<string>("name"), r.Get<string>("description"), r.Get<string>("category"), r.Get<string>("chart"))
        {
            Parameters = JsonSerializer.Deserialize<List<InputField>>(r.Get<string>("parameters"), GreenHiveJson.Options) ?? [],
            Permission = r.Get<string>("permission"),
        };
}
