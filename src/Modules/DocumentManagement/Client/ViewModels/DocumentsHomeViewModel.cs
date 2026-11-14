using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Modules.DocumentManagement.Client.ViewModels;

/// <summary>Module home: key figures and the invoices waiting for a decision.</summary>
public sealed class DocumentsHomeViewModel : ModuleViewModel
{
    private DocumentStats? _stats;
    private IReadOnlyList<InvoiceDto> _waiting = [];

    public DocumentStats? Stats
    {
        get => _stats;
        private set => SetProperty(ref _stats, value);
    }

    public IReadOnlyList<InvoiceDto> Waiting
    {
        get => _waiting;
        private set => SetProperty(ref _waiting, value);
    }

    protected override void OnCreated()
    {
        Title = T("view.home.label");
        Module.Subscribe("documents.*", e => AppContext.Dispatcher?.Run(() => _ = LoadAsync()));
    }

    public override void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    public async Task LoadAsync()
    {
        if (Closed.IsCancellationRequested)
        {
            return;
        }

        await RunAsync(ct => Api.GetAsync<DocumentStats>("stats", ct), s => Stats = s);
        await RunAsync(ct => Api.GetAsync<IReadOnlyList<InvoiceDto>>($"invoices?status={InvoiceStatus.Submitted}", ct), list => Waiting = list);
    }

    public void OpenInvoice(InvoiceDto invoice) => Module.NavigateTo(DocumentManagementModule.Invoices, invoice.Id.ToString());

    public void Open(string key) => Module.NavigateTo(key);
}
