using System.Globalization;
using GreenHive.NET.Modules.DocumentManagement.Client.ViewModels;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Contracts.Api;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using System.Windows;
using GreenHive.Wpf.Framework.Theming;
using System.Windows.Controls;
using System.Windows.Media;

namespace GreenHive.NET.Modules.DocumentManagement.Client.Views;

/// <summary>Invoices: status filter, list, details with the actions the current status and the user's permissions allow.</summary>
public sealed class InvoiceListView : UserControl
{
    private static readonly double[] Columns = [1.2, 2.2, 1.2, 1, 1, 1.2];
    private static readonly string[] Statuses = [InvoiceStatus.Draft, InvoiceStatus.Submitted, InvoiceStatus.Approved, InvoiceStatus.Rejected, InvoiceStatus.Paid];

    private readonly InvoiceListViewModel _vm;
    private readonly DataList<InvoiceDto> _list;
    private readonly SpacedPanel _detail = new() { Spacing = 12, Padding = new Thickness(16) };

    public InvoiceListView(InvoiceListViewModel vm)
    {
        _vm = vm;
        var header = DataList<InvoiceDto>.Cells(Columns,
            UiKit.Label(vm.T("field.number")), UiKit.Label(vm.T("field.supplier")), UiKit.Label(vm.T("field.amount")),
            UiKit.Label(vm.T("field.costCenter")), UiKit.Label(vm.T("field.dueOn")), UiKit.Label(vm.T("field.status")));
        _list = new DataList<InvoiceDto>(Row, i => i.Id, vm.T("invoices.empty"), header);
        _list.SelectionChanged += (_, i) =>
        {
            if (i?.Id != vm.Selected?.Id)
            {
                vm.Selected = i;
            }
        };

        var search = new SearchBox { PlaceholderText = vm.T("search"), Width = 240 };
        search.QuerySubmitted += async (_, e) =>
        {
            vm.Search = e.QueryText;
            await vm.LoadAsync();
        };
        var status = new ComboBox { Width = 170 };
        status.Items.Add(new ComboBoxItem { Content = vm.T("all"), Tag = string.Empty });
        foreach (var s in Statuses)
        {
            status.Items.Add(new ComboBoxItem { Content = vm.T("status." + s), Tag = s });
        }

        status.SelectedIndex = 0;
        status.SelectionChanged += async (_, _) =>
        {
            vm.Status = (status.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            await vm.LoadAsync();
        };
        var create = UiKit.Button(vm.T("new"), async (_, _) => await CreateAsync(), accent: true, glyph: "\uE710");
        create.IsEnabled = vm.CanEdit;
        var toolbar = UiKit.Row(search, status, UiKit.Button(vm.T("refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), create, UiKit.Busy(vm));
        toolbar.Margin = new Thickness(12, 8, 12, 0);

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 360 });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        var detail = new ScrollViewer { Content = _detail };
        Grid.SetColumn(detail, 1);
        layout.Children.Add(_list.View);
        layout.Children.Add(detail);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var messages = UiKit.Messages(vm);
        messages.Margin = new Thickness(12, 4, 12, 0);
        Grid.SetRow(messages, 1);
        Grid.SetRow(layout, 2);
        root.Children.Add(toolbar);
        root.Children.Add(messages);
        root.Children.Add(layout);
        Content = root;

        UiKit.Watch(vm, nameof(InvoiceListViewModel.Items), () =>
        {
            _list.SetItems(vm.Items);
            if (vm.Selected is { } selected)
            {
                _list.Select(i => i.Id == selected.Id);
            }
        });
        UiKit.Watch(vm, nameof(InvoiceListViewModel.Selected), () =>
        {
            if (vm.Selected is { } selected && _list.SelectedItem?.Id != selected.Id)
            {
                _list.Select(i => i.Id == selected.Id);
            }

            ShowDetail();
        });
        UiKit.Watch(vm, nameof(InvoiceListViewModel.CreateRequested), () =>
        {
            if (vm.CreateRequested)
            {
                vm.CreateRequested = false;
                Dispatcher.BeginInvoke(new Action(async () => await CreateAsync()));
            }
        });
    }

    private static bool IsOverdue(InvoiceDto i) =>
        i.Status is InvoiceStatus.Submitted or InvoiceStatus.Approved && i.DueOn < DateOnly.FromDateTime(DateTime.Today);

    private FrameworkElement Row(InvoiceDto i)
    {
        var due = UiKit.Text(i.DueOn.ToString("d", CultureInfo.CurrentCulture));
        if (IsOverdue(i))
        {
            due.Foreground = Theme.Error;
        }

        var amount = UiKit.Text(UiKit.Money(i.Amount, i.Currency));
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        return DataList<InvoiceDto>.Cells(Columns, UiKit.Text(i.Number), UiKit.Text(i.Supplier), amount, UiKit.Text(i.CostCenter), due, UiKit.Text(_vm.T("status." + i.Status)));
    }

    private void ShowDetail()
    {
        _detail.Children.Clear();
        if (_vm.Selected is not { } i)
        {
            _detail.Children.Add(UiKit.Caption(_vm.T("invoices.select")));
            return;
        }

        _detail.Children.Add(UiKit.Title($"{i.Number} - {i.Supplier}"));
        _detail.Children.Add(new TextBlock { Text = UiKit.Money(i.Amount, i.Currency), FontSize = 28, FontWeight = FontWeights.SemiBold });
        var facts = new SpacedGrid { ColumnSpacing = 16, RowSpacing = 6 };
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Fact(string label, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var row = facts.RowDefinitions.Count;
            facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = UiKit.Caption(label);
            var v = UiKit.Text(value, wrap: true);
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            facts.Children.Add(l);
            facts.Children.Add(v);
        }

        Fact(_vm.T("field.status"), _vm.T("status." + i.Status) + (IsOverdue(i) ? $" ({_vm.T("invoices.overdue")})" : string.Empty));
        Fact(_vm.T("field.costCenter"), i.CostCenter);
        Fact(_vm.T("field.issuedOn"), i.IssuedOn.ToString("d", CultureInfo.CurrentCulture));
        Fact(_vm.T("field.dueOn"), i.DueOn.ToString("d", CultureInfo.CurrentCulture));
        Fact(_vm.T("field.submittedBy"), i.SubmittedByName is null ? null : $"{i.SubmittedByName} · {i.SubmittedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}");
        Fact(_vm.T("field.decidedBy"), i.DecidedByName is null ? null : $"{i.DecidedByName} · {i.DecidedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}");
        Fact(_vm.T("field.comment"), i.Comment);
        _detail.Children.Add(UiKit.Card(facts, 12));

        var actions = new SpacedPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (_vm.CanSubmit)
        {
            actions.Children.Add(UiKit.Button(_vm.T("invoices.submit"), async (_, _) => await _vm.SubmitAsync(), accent: true, glyph: "\uE724"));
        }

        if (_vm.CanDecide)
        {
            actions.Children.Add(UiKit.Button(_vm.T("invoices.approve"), async (_, _) => await DecideAsync(true), accent: true, glyph: "\uE8FB"));
            actions.Children.Add(UiKit.Button(_vm.T("invoices.reject"), async (_, _) => await DecideAsync(false), glyph: "\uE711"));
        }

        if (_vm.CanPay)
        {
            actions.Children.Add(UiKit.Button(_vm.T("invoices.pay"), async (_, _) => await _vm.PayAsync(), glyph: "\uE73E"));
        }

        _detail.Children.Add(actions);
    }

    private async Task DecideAsync(bool approve)
    {
        var comment = new TextBox { AcceptsReturn = true, MinHeight = 80, TextWrapping = TextWrapping.Wrap, MinWidth = 380 };
        var title = _vm.T(approve ? "invoices.approve" : "invoices.reject");
        await UiKit.DialogAsync(this, title, UiKit.Field(_vm.T("field.comment"), comment), title, _vm.T("cancel"), () =>
        {
            var text = string.IsNullOrWhiteSpace(comment.Text) ? null : comment.Text.Trim();
            return approve ? _vm.ApproveAsync(text) : _vm.RejectAsync(text);
        });
    }

    private async Task CreateAsync()
    {
        var number = new HintTextBox { PlaceholderText = $"INV-{DateTime.Today:yyyy}-" };
        var supplier = new TextBox();
        var amount = new NumberBox { Minimum = 0, SmallChange = 10, LargeChange = 100, Value = double.NaN };
        var currency = new ComboBox { ItemsSource = new[] { "EUR", "CHF", "USD", "GBP" }, SelectedIndex = 0 };
        var issued = new CalendarDatePicker { Date = DateTimeOffset.Now };
        var due = new CalendarDatePicker { Date = DateTimeOffset.Now.AddDays(30) };
        var costCenter = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, DisplayMemberPath = nameof(LookupItem.Label) };
        var errors = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Theme.Error };

        var centers = await _vm.CostCentersAsync();
        if (centers.TryGetValue(out var items))
        {
            costCenter.ItemsSource = items;
            costCenter.SelectedIndex = items.Count > 0 ? 0 : -1;
        }

        var form = UiKit.Column(
            UiKit.Field(_vm.T("field.number"), number), UiKit.Field(_vm.T("field.supplier"), supplier),
            UiKit.Row(UiKit.Field(_vm.T("field.amount"), amount), UiKit.Field(_vm.T("field.currency"), currency)),
            UiKit.Row(UiKit.Field(_vm.T("field.issuedOn"), issued), UiKit.Field(_vm.T("field.dueOn"), due)),
            UiKit.Field(_vm.T("field.costCenter"), costCenter), errors);
        form.MinWidth = 440;
        await UiKit.DialogAsync(this, _vm.T("cmd.createInvoice.label").TrimEnd('.'), form, _vm.T("save"), _vm.T("cancel"), async () =>
        {
            var valid = _vm.Validate(number.Text, supplier.Text, amount.Value, currency.SelectedItem as string ?? "EUR",
                issued.Date ?? DateTimeOffset.Now, due.Date ?? DateTimeOffset.Now.AddDays(30), (costCenter.SelectedItem as LookupItem)?.Value);
            if (!valid.TryGetValue(out var request))
            {
                errors.Text = string.Join("\n", valid.Errors.Select(e => e.Message));
                return false;
            }

            return await _vm.CreateAsync(request);
        });
    }
}
