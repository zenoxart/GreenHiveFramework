using System.Globalization;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.WorkflowContracts;
using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>Reports (left) and the selected report: surface diff and affected workflows with severity and reasons.</summary>
public sealed class WorkflowImpactView : UserControl
{
    private readonly WorkflowImpactViewModel _vm;
    private readonly DataList<WorkflowImpactReport> _list;
    private readonly SpacedPanel _detail = new() { Spacing = 12, Padding = new Thickness(16) };

    public WorkflowImpactView(WorkflowImpactViewModel vm)
    {
        _vm = vm;
        _list = new DataList<WorkflowImpactReport>(r => UiKit.Column(
                UiKit.Label($"{r.ModuleId} {r.FromVersion} → {r.ToVersion}"),
                UiKit.Caption($"{r.AnalyzedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} · {r.Workflows.Count} workflows · {r.BrokenCount} broken · {r.ReviewCount} review")),
            r => r.Id, vm.T("workflow:impact.empty"));
        _list.SelectionChanged += (_, r) =>
        {
            if (r is not null)
            {
                vm.Selected = r;
            }
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 240 });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        var scroll = new ScrollViewer { Content = _detail };
        Grid.SetColumn(scroll, 1);
        layout.Children.Add(_list.View);
        layout.Children.Add(scroll);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var top = UiKit.Column(UiKit.Row(UiKit.Button(vm.T("workflow:inbox.refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), UiKit.Busy(vm)), UiKit.Messages(vm));
        top.Margin = new Thickness(12, 8, 12, 8);
        Grid.SetRow(layout, 1);
        root.Children.Add(top);
        root.Children.Add(layout);
        Content = root;

        UiKit.Watch(vm, nameof(WorkflowImpactViewModel.Reports), () => _list.SetItems(vm.Reports));
        UiKit.Watch(vm, nameof(WorkflowImpactViewModel.Selected), ShowDetail);
    }

    private void ShowDetail()
    {
        _detail.Children.Clear();
        if (_vm.Selected is not { } report)
        {
            return;
        }

        _detail.Children.Add(UiKit.Title($"{report.ModuleId} {report.FromVersion} → {report.ToVersion}"));
        var diff = UiKit.Column(UiKit.Label(_vm.T("workflow:impact.diff")));
        foreach (var change in report.SurfaceDiff)
        {
            diff.Children.Add(UiKit.Caption($"{(change.IsBreaking ? "⚠ " : string.Empty)}{change.Kind} {change.Id}: {change.Change} - {change.Detail}"));
        }

        _detail.Children.Add(UiKit.Card(diff));
        _detail.Children.Add(UiKit.Label(_vm.T("workflow:impact.workflows")));
        foreach (var impact in report.Workflows.OrderByDescending(w => w.Severity))
        {
            var severity = impact.Severity switch
            {
                ImpactSeverity.Broken => MessageSeverity.Error,
                ImpactSeverity.ReviewRequired => MessageSeverity.Warning,
                ImpactSeverity.AutoMigrated => MessageSeverity.Success,
                _ => MessageSeverity.Info,
            };
            var lines = impact.Reasons.Select(r => $"{r.StepId}: {r.Reason}").ToList();
            if (impact.MigratedToVersion is { } version)
            {
                lines.Add(_vm.T("workflow:impact.migrated", ("version", version)));
            }

            if (impact.AiTriggersSuspended)
            {
                lines.Add(_vm.T("workflow:impact.suspended"));
            }

            if (impact.RunningInstances > 0)
            {
                lines.Add(_vm.T("workflow:impact.running", ("count", impact.RunningInstances), ("action", impact.InstanceAction)));
            }

            var open = UiKit.Link(_vm.T("workflow:impact.open"), (_, _) => _vm.OpenWorkflow(impact.WorkflowId));
            var bar = MessageBar.Open(severity, $"{impact.Name} (v{impact.Version}) - {impact.Severity}", string.Join("\n", lines));
            bar.Action = open;
            _detail.Children.Add(bar);
        }
    }
}
