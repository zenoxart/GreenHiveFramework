using System.Globalization;
using GreenHive.NET.Modules.DocumentManagement.Client.ViewModels;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using System.Windows;
using GreenHive.Wpf.Framework.Theming;
using System.Windows.Controls;

namespace GreenHive.NET.Modules.DocumentManagement.Client.Views;

/// <summary>Module home: tiles with key figures and the invoices waiting for a decision.</summary>
public sealed class DocumentsHomeView : UserControl
{
    private readonly DocumentsHomeViewModel _vm;
    private readonly WrapPanel _tiles = new() { Orientation = Orientation.Horizontal, ItemWidth = 210, ItemHeight = 104 };
    private readonly SpacedPanel _waiting = new() { Spacing = 6 };

    public DocumentsHomeView(DocumentsHomeViewModel vm)
    {
        _vm = vm;
        var root = new SpacedPanel { Spacing = 16, Padding = new Thickness(24) };
        root.Children.Add(UiKit.Row(
            UiKit.Title(vm.T("view.home.label")),
            UiKit.Button(vm.T("refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"),
            UiKit.Busy(vm)));
        root.Children.Add(UiKit.Messages(vm));
        root.Children.Add(_tiles);
        root.Children.Add(UiKit.Row(
            UiKit.Button(vm.T("view.documents.label"), (_, _) => vm.Open(DocumentManagementModule.Documents), glyph: "\uE8F1"),
            UiKit.Button(vm.T("view.invoices.label"), (_, _) => vm.Open(DocumentManagementModule.Invoices), glyph: "\uE8EF")));
        root.Children.Add(UiKit.Label(vm.T("home.waiting")));
        root.Children.Add(_waiting);
        Content = new ScrollViewer { Content = root };

        UiKit.Watch(vm, nameof(DocumentsHomeViewModel.Stats), ShowStats);
        UiKit.Watch(vm, nameof(DocumentsHomeViewModel.Waiting), ShowWaiting);
    }

    private void ShowStats()
    {
        _tiles.Children.Clear();
        if (_vm.Stats is not { } s)
        {
            return;
        }

        Tile(_vm.T("stats.documents"), s.Documents.ToString(CultureInfo.CurrentCulture));
        Tile(_vm.T("stats.drafts"), s.Drafts.ToString(CultureInfo.CurrentCulture));
        Tile(_vm.T("stats.archived"), s.Archived.ToString(CultureInfo.CurrentCulture));
        Tile(_vm.T("stats.submitted"), s.InvoicesSubmitted.ToString(CultureInfo.CurrentCulture));
        Tile(_vm.T("stats.approved"), s.InvoicesApproved.ToString(CultureInfo.CurrentCulture));
        Tile(_vm.T("stats.open"), UiKit.Money(s.OpenAmount, "EUR"));
        Tile(_vm.T("stats.overdue"), s.OverdueInvoices.ToString(CultureInfo.CurrentCulture));
    }

    private void Tile(string caption, string value)
    {
        var panel = UiKit.Column(UiKit.Caption(caption), new TextBlock { Text = value, FontSize = 28, FontWeight = FontWeights.SemiBold });
        var card = UiKit.Card(panel, 14);
        card.Margin = new Thickness(0, 0, 10, 10);
        _tiles.Children.Add(card);
    }

    private void ShowWaiting()
    {
        _waiting.Children.Clear();
        if (_vm.Waiting.Count == 0)
        {
            _waiting.Children.Add(UiKit.Caption(_vm.T("home.none")));
            return;
        }

        foreach (var invoice in _vm.Waiting.OrderBy(i => i.DueOn).Take(15))
        {
            var link = new HyperlinkButton
            {
                Content = $"{invoice.Number}  ·  {invoice.Supplier}  ·  {UiKit.Money(invoice.Amount, invoice.Currency)}  ·  {invoice.DueOn.ToString("d", CultureInfo.CurrentCulture)}" +
                          (invoice.SubmittedByName is null ? string.Empty : $"  ·  {invoice.SubmittedByName}"),
            };
            link.Click += (_, _) => _vm.OpenInvoice(invoice);
            _waiting.Children.Add(link);
        }
    }
}
