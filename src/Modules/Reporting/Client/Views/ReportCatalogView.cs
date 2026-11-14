using System.Globalization;
using GreenHive.NET.Modules.Reporting.Client.ViewModels;
using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using System.Windows;
using GreenHive.Wpf.Framework.Theming;
using System.Windows.Controls;

namespace GreenHive.NET.Modules.Reporting.Client.Views;

/// <summary>Catalogue (left); parameters, chart and table of the selected report (right).</summary>
public sealed class ReportCatalogView : UserControl
{
    private readonly ReportCatalogViewModel _vm;
    private readonly DataList<ReportDefinitionDto> _list;
    private readonly SpacedPanel _detail = new() { Spacing = 12, Padding = new Thickness(16) };
    private readonly SpacedPanel _result = new() { Spacing = 12 };
    private readonly SimpleChart _chart = new() { Height = 240 };
    private InputForm? _form;

    public ReportCatalogView(ReportCatalogViewModel vm)
    {
        _vm = vm;
        _list = new DataList<ReportDefinitionDto>(r =>
        {
            var column = UiKit.Column(UiKit.Label(r.Name), UiKit.Caption($"{r.Category} · {r.Description}"));
            column.Spacing = 2;
            column.Padding = new Thickness(0, 6, 0, 6);
            return column;
        }, r => r.Id, vm.T("empty"));
        _list.SelectionChanged += (_, r) =>
        {
            if (r is not null && r.Id != vm.Selected?.Id)
            {
                vm.Selected = r;
            }
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        var detail = new ScrollViewer { Content = _detail };
        Grid.SetColumn(detail, 1);
        layout.Children.Add(_list.View);
        layout.Children.Add(detail);

        var top = UiKit.Column(UiKit.Row(UiKit.Button(vm.T("refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), UiKit.Busy(vm)), UiKit.Messages(vm));
        top.Margin = new Thickness(12, 8, 12, 4);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(layout, 1);
        root.Children.Add(top);
        root.Children.Add(layout);
        Content = root;

        UiKit.Watch(vm, nameof(ReportCatalogViewModel.Reports), () =>
        {
            _list.SetItems(vm.Reports);
            if (vm.Selected is { } s)
            {
                _list.Select(r => r.Id == s.Id);
            }
        });
        UiKit.Watch(vm, nameof(ReportCatalogViewModel.Selected), ShowReport);
        UiKit.Watch(vm, nameof(ReportCatalogViewModel.Result), ShowResult);
    }

    private void ShowReport()
    {
        _detail.Children.Clear();
        _form = null;
        if (_vm.Selected is not { } report)
        {
            _detail.Children.Add(UiKit.Caption(_vm.T("select")));
            return;
        }

        if (_list.SelectedItem?.Id != report.Id)
        {
            _list.Select(r => r.Id == report.Id);
        }

        _detail.Children.Add(UiKit.Title(report.Name));
        _detail.Children.Add(UiKit.Caption(report.Description));
        _form = new InputForm(_vm.Schema(report), _vm.AppContext.Languages, null, ReportCatalogViewModel.Defaults(report));
        var run = UiKit.Button(_vm.T("run"), async (_, _) => await RunAsync(), accent: true, glyph: "\uE768");
        run.IsEnabled = _vm.CanRun;
        var parameters = UiKit.Column(UiKit.Label(_vm.T("parameters")), _form, run);
        if (!_vm.CanRun)
        {
            parameters.Children.Add(UiKit.Caption(_vm.T("noPermission")));
        }

        _detail.Children.Add(UiKit.Card(parameters, 12));
        _detail.Children.Add(_result);
        ShowResult();
    }

    private async Task RunAsync()
    {
        if (_form is null)
        {
            return;
        }

        var values = _form.Validate();
        if (values.TryGetValue(out var parameters))
        {
            await _vm.ExecuteAsync(parameters);
        }
    }

    private void ShowResult()
    {
        _result.Children.Clear();
        if (_vm.Result is not { } r)
        {
            return;
        }

        var header = UiKit.Row(UiKit.Label(_vm.T("result")), UiKit.Caption(_vm.T("rows", ("count", r.Rows.Count))));
        if (r.Total is { } total)
        {
            header.Children.Add(UiKit.Caption(_vm.T("total", ("total", total.ToString("N2", CultureInfo.CurrentCulture)))));
        }

        header.Children.Add(UiKit.Button(_vm.T("export"), (_, _) =>
        {
            Clipboard.SetText(_vm.ToCsv());
            _vm.Copied();
        }, glyph: "\uE8C8"));
        _result.Children.Add(header);

        if (r.Series.Count > 0 && r.Chart != "none")
        {
            _chart.SetValues(r.Series.Select(p => new ChartValue(p.Label, p.Value)).ToList(), line: r.Chart == "line");
            _result.Children.Add(UiKit.Card(UiKit.Column(UiKit.Caption(r.SeriesLabel), _chart), 12));
        }

        _result.Children.Add(UiKit.Card(Table(r), 8));
    }

    private static FrameworkElement Table(ReportResult r)
    {
        var grid = new SpacedGrid { ColumnSpacing = 16, RowSpacing = 4 };
        foreach (var _ in r.Columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        void Cell(UIElement element, int row, int column, ReportColumn definition)
        {
            if (element is FrameworkElement fe && definition.Kind is ReportColumnKind.Number or ReportColumnKind.Currency)
            {
                fe.HorizontalAlignment = HorizontalAlignment.Right;
            }

            Grid.SetRow((FrameworkElement)element, row);
            Grid.SetColumn((FrameworkElement)element, column);
            grid.Children.Add(element);
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < r.Columns.Count; c++)
        {
            Cell(UiKit.Label(r.Columns[c].Label), 0, c, r.Columns[c]);
        }

        for (var i = 0; i < r.Rows.Count && i < 500; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = r.Rows[i];
            for (var c = 0; c < r.Columns.Count && c < row.Count; c++)
            {
                Cell(UiKit.Text(ReportCatalogViewModel.Display(r.Columns[c], row[c])), i + 1, c, r.Columns[c]);
            }
        }

        return new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
}
