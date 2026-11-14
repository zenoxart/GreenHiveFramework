using GreenHive.NET.Modules.Reporting.Client.ViewModels;
using GreenHive.NET.Modules.Reporting.Client.Views;
using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.NET.CommandContracts;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;

namespace GreenHive.NET.Modules.Reporting.Client;

/// <summary>Entry point of the reporting module.</summary>
public sealed class ReportingModule : ModuleEntryPoint
{
    public const string Catalog = "reports.catalog";
    public const string Runs = "reports.runs";

    protected override void OnInitialize()
    {
        Texts("en", ReportTexts.English);
        Texts("de", ReportTexts.German);
        Icon("home", "E9D9");
        Icon("runs", "E81C");
        Icon("run", "E768");

        View<ReportCatalogViewModel>(Catalog, "reports:view.catalog", "reports.home", vm => new ReportCatalogView(vm), home: true, order: 0, permission: ReportPermissions.View);
        View<ReportRunsViewModel>(Runs, "reports:view.runs", "reports.runs", vm => new ReportRunsView(vm),
            new ViewDescriptor { Role = ViewRole.ToolWindow, DefaultDock = DockSide.Bottom, MinHeight = 160 }, order: 1, permission: ReportPermissions.View);

        MenuGroup("reports:menu", order: 30);
        NavigationGroup("reports:module", order: 30);
        Command("reports.open", "reports:cmd.open", "reports.home", _ => Go(Catalog), permission: ReportPermissions.View, placements: new CommandPlacement[] { InMenu(0, 0) });
        Command("reports.runs.open", "reports:cmd.runs", "reports.runs", _ => Go(Runs), permission: ReportPermissions.View, placements: new CommandPlacement[] { InMenu(0, 1) });
        Command("reports.run", "reports:cmd.run", "reports.run", parameter => Go(Catalog, parameter as string), permission: ReportPermissions.Run,
            workflowCapable: true, aiAllowed: true);
    }

    private Task Go(string key, string? parameter = null)
    {
        Module.NavigateTo(key, parameter);
        return Task.CompletedTask;
    }
}
