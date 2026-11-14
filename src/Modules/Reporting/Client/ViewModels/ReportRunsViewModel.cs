using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;

namespace GreenHive.NET.Modules.Reporting.Client.ViewModels;

/// <summary>Recent report runs (refreshes on reports.ready).</summary>
public sealed class ReportRunsViewModel : ModuleViewModel
{
    private IReadOnlyList<ReportRunDto> _runs = [];

    public IReadOnlyList<ReportRunDto> Runs
    {
        get => _runs;
        private set => SetProperty(ref _runs, value);
    }

    protected override void OnCreated()
    {
        Title = T("view.runs.label");
        Module.Subscribe("reports.ready", e => AppContext.Dispatcher?.Run(() => _ = LoadAsync()));
    }

    public override void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    public Task<bool> LoadAsync() => RunAsync(ct => Api.GetAsync<IReadOnlyList<ReportRunDto>>("runs", ct), r => Runs = r);

    public void Open(ReportRunDto run) => Module.NavigateTo(ReportingModule.Catalog, run.ReportId);
}
