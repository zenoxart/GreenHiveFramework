using System.Globalization;
using GreenHive.NET.Modules.Reporting.Client.ViewModels;
using GreenHive.NET.Modules.Reporting.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using System.Windows;
using GreenHive.Wpf.Framework.Theming;
using System.Windows.Controls;

namespace GreenHive.NET.Modules.Reporting.Client.Views;

/// <summary>Tool window with the recent report runs; double-click opens the report.</summary>
public sealed class ReportRunsView : UserControl
{
    private static readonly double[] Columns = [2, 1, 1.4, 1.4, 0.6];

    public ReportRunsView(ReportRunsViewModel vm)
    {
        var list = new DataList<ReportRunDto>(r => DataList<ReportRunDto>.Cells(Columns,
                UiKit.Text(r.ReportName), UiKit.Text(r.Status), UiKit.Text(r.RequestedByName),
                UiKit.Text(r.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)), UiKit.Text(r.RowCount.ToString(CultureInfo.CurrentCulture))),
            r => r.Id, vm.T("runs.empty"));
        list.ItemInvoked += (_, r) => vm.Open(r);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var top = UiKit.Column(UiKit.Row(UiKit.Button(vm.T("refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), UiKit.Busy(vm)), UiKit.Messages(vm));
        top.Margin = new Thickness(8);
        Grid.SetRow(list.View, 1);
        root.Children.Add(top);
        root.Children.Add(list.View);
        Content = root;
        UiKit.Watch(vm, nameof(ReportRunsViewModel.Runs), () => list.SetItems(vm.Runs));
    }
}
