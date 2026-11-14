using GreenHive.NET.Modules.DocumentManagement.Client.ViewModels;
using GreenHive.NET.Modules.DocumentManagement.Client.Views;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.CommandContracts;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;

namespace GreenHive.NET.Modules.DocumentManagement.Client;

/// <summary>Entry point of the document-management module.</summary>
public sealed class DocumentManagementModule : ModuleEntryPoint
{
    public const string Home = "documents.home";
    public const string Documents = "documents.list";
    public const string Invoices = "documents.invoices";

    protected override void OnInitialize()
    {
        Texts("en", DocumentTexts.English);
        Texts("de", DocumentTexts.German);

        Icon("home", "E8A5");
        Icon("list", "E8F1");
        Icon("invoice", "E8EF");
        Icon("new", "E710");
        Icon("refresh", "E72C");

        View<DocumentsHomeViewModel>(Home, "documents:view.home", "documents.home", vm => new DocumentsHomeView(vm), home: true, order: 0, permission: DocumentPermissions.View);
        View<DocumentListViewModel>(Documents, "documents:view.documents", "documents.list", vm => new DocumentListView(vm), order: 1, permission: DocumentPermissions.View);
        View<InvoiceListViewModel>(Invoices, "documents:view.invoices", "documents.invoice", vm => new InvoiceListView(vm), order: 2, permission: DocumentPermissions.View);

        MenuGroup("documents:menu", order: 20);
        NavigationGroup("documents:module", order: 20);
        Command("documents.open", "documents:cmd.open", "documents.list", _ => Navigate(Documents), permission: DocumentPermissions.View,
            placements: new CommandPlacement[] { InMenu(0, 0) });
        Command("documents.invoices.open", "documents:cmd.invoices", "documents.invoice", _ => Navigate(Invoices), permission: DocumentPermissions.View,
            placements: new CommandPlacement[] { InMenu(0, 1) });
        Command("documents.create", "documents:cmd.create", "documents.new", _ => Navigate(Documents, DocumentListViewModel.CreateParameter),
            permission: DocumentPermissions.Edit, placements: new CommandPlacement[] { InMenu(1, 0) }, workflowCapable: true);
        Command("documents.invoice.create", "documents:cmd.createInvoice", "documents.new", _ => Navigate(Invoices, InvoiceListViewModel.CreateParameter),
            permission: DocumentPermissions.Edit, placements: new CommandPlacement[] { InMenu(1, 1) }, workflowCapable: true);
    }

    private Task Navigate(string key, string? parameter = null)
    {
        Module.NavigateTo(key, parameter);
        return Task.CompletedTask;
    }
}
