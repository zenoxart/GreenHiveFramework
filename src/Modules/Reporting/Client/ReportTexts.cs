namespace GreenHive.NET.Modules.Reporting.Client;

/// <summary>Texts of the module (scope "reports").</summary>
internal static class ReportTexts
{
    public static readonly Dictionary<string, string> English = new()
    {
        ["module.name"] = "Reports", ["module.label"] = "Reports",
        ["menu.label"] = "_Reports",
        ["view.catalog"] = "Reports", ["view.catalog.label"] = "Reports", ["view.catalog.tooltip"] = "Report catalogue with parameters, table and chart",
        ["view.runs"] = "Report runs", ["view.runs.label"] = "Report runs", ["view.runs.tooltip"] = "Recent report runs",
        ["cmd.open.label"] = "Reports", ["cmd.open.tooltip"] = "Open the report catalogue",
        ["cmd.runs.label"] = "Report runs", ["cmd.runs.tooltip"] = "Show recent report runs",
        ["cmd.run.label"] = "Run report", ["cmd.run.tooltip"] = "Run a report (parameter: report ID)",
        ["step.runReport"] = "Run report", ["field.report"] = "Report", ["field.parameters"] = "Parameters (JSON)",
        ["refresh"] = "Refresh", ["run"] = "Run", ["empty"] = "No reports available.", ["select"] = "Select a report.",
        ["parameters"] = "Parameters", ["result"] = "Result", ["rows"] = "{count} rows", ["total"] = "Total: {total}",
        ["runs.empty"] = "No runs yet.", ["export"] = "Copy as CSV", ["copied"] = "The table was copied to the clipboard.",
        ["noPermission"] = "You may view reports but not run them.",
        ["notification.ready"] = "Report ready", ["report.notFound"] = "The report was not found.", ["run.notFound"] = "The report run was not found.",
    };

    public static readonly Dictionary<string, string> German = new()
    {
        ["module.name"] = "Berichte", ["module.label"] = "Berichte",
        ["menu.label"] = "_Berichte",
        ["view.catalog"] = "Berichte", ["view.catalog.label"] = "Berichte", ["view.catalog.tooltip"] = "Berichtskatalog mit Parametern, Tabelle und Diagramm",
        ["view.runs"] = "Berichtsläufe", ["view.runs.label"] = "Berichtsläufe", ["view.runs.tooltip"] = "Letzte Berichtsläufe",
        ["cmd.open.label"] = "Berichte", ["cmd.open.tooltip"] = "Berichtskatalog öffnen",
        ["cmd.runs.label"] = "Berichtsläufe", ["cmd.runs.tooltip"] = "Letzte Berichtsläufe anzeigen",
        ["cmd.run.label"] = "Bericht ausführen", ["cmd.run.tooltip"] = "Einen Bericht ausführen (Parameter: Berichts-ID)",
        ["step.runReport"] = "Bericht ausführen", ["field.report"] = "Bericht", ["field.parameters"] = "Parameter (JSON)",
        ["refresh"] = "Aktualisieren", ["run"] = "Ausführen", ["empty"] = "Keine Berichte verfügbar.", ["select"] = "Wählen Sie einen Bericht.",
        ["parameters"] = "Parameter", ["result"] = "Ergebnis", ["rows"] = "{count} Zeilen", ["total"] = "Summe: {total}",
        ["runs.empty"] = "Noch keine Läufe.", ["export"] = "Als CSV kopieren", ["copied"] = "Die Tabelle wurde in die Zwischenablage kopiert.",
        ["noPermission"] = "Sie dürfen Berichte ansehen, aber nicht ausführen.",
        ["notification.ready"] = "Bericht fertig", ["report.notFound"] = "Der Bericht wurde nicht gefunden.", ["run.notFound"] = "Der Berichtslauf wurde nicht gefunden.",
    };
}
