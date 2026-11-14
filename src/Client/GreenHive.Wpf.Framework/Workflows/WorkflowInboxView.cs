using System.Globalization;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.Wpf.Framework.Security;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;
using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>Inbox: list of tasks on the left, the selected task on the right (stacked in the compact size class).</summary>
public sealed class WorkflowInboxView : UserControl
{
    private readonly WorkflowInboxViewModel _vm;
    private readonly DataList<InboxItem> _list;
    private readonly Border _detail = new();
    private readonly Grid _layout = new();

    public WorkflowInboxView(WorkflowInboxViewModel vm)
    {
        _vm = vm;
        _list = new DataList<InboxItem>(Row, i => i.InstanceId, vm.T("workflow:inbox.empty"));
        _list.SelectionChanged += async (_, item) => await vm.SelectAsync(item);

        var start = UiKit.MenuButton(vm.T("workflow:inbox.start"), () =>
            vm.Startable.Select(workflow => (workflow.Name, (Action)(async () => await vm.StartAsync(workflow, WorkflowExecutor.Human)))).ToList());

        var toolbar = UiKit.Row(UiKit.Button(vm.T("workflow:inbox.refresh"), async (_, _) => await vm.RefreshAsync(), glyph: "\uE72C"), start, UiKit.Busy(vm));
        toolbar.Margin = new Thickness(12, 8, 12, 8);

        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 260 });
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        Grid.SetColumn(_detail, 1);
        _layout.Children.Add(_list.View);
        _layout.Children.Add(_detail);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var messages = UiKit.Messages(vm);
        messages.Margin = new Thickness(12, 0, 12, 0);
        Grid.SetRow(messages, 1);
        Grid.SetRow(_layout, 2);
        root.Children.Add(toolbar);
        root.Children.Add(messages);
        root.Children.Add(_layout);
        Content = root;

        vm.ItemsChanged += (_, _) => _list.SetItems(vm.Items);
        _list.SetItems(vm.Items);
        UiKit.Watch(vm, nameof(WorkflowInboxViewModel.Details), ShowDetail);
        UiKit.Watch(vm, nameof(WorkflowInboxViewModel.Selected), ShowDetail);
        UiKit.Watch(vm, nameof(WorkflowInboxViewModel.SizeClass), ApplySize);
    }

    private FrameworkElement Row(InboxItem item)
    {
        var badge = item.Proposal is not null ? _vm.T("workflow:inbox.badge.ai")
            : item.StepKind == WorkflowStepKind.Approval ? _vm.T("workflow:inbox.badge.approval")
            : item.LastError is not null ? _vm.T("workflow:inbox.badge.problem")
            : string.Empty;
        var column = UiKit.Column(
            UiKit.Label($"{item.WorkflowName}"),
            UiKit.Text(_vm.Text(item.StepTitle)),
            UiKit.Caption($"{WorkflowInboxViewModel.Describe(item.Variables)}"),
            UiKit.Caption($"{item.Since.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}   {badge}"));
        column.Spacing = 2;
        column.Padding = new Thickness(0, 6, 0, 6);
        return column;
    }

    private void ShowDetail()
    {
        _detail.Child = _vm.Selected is { } item
            ? WorkflowStepPresenter.Build(_vm, item, ((ISecurityContext)_vm.AppContext).Backend)
            : new TextBlock { Text = _vm.T("workflow:inbox.select"), Opacity = 0.6, Margin = new Thickness(24) };
    }

    /// <summary>Side by side from Medium on; list above the task in the compact size class.</summary>
    private void ApplySize()
    {
        var compact = _vm.SizeClass == GreenHive.Wpf.Framework.Mvvm.SizeClass.Compact;
        _layout.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
        if (_layout.RowDefinitions.Count == 0)
        {
            _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });
        }

        _layout.RowDefinitions[1].Height = compact ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(_detail, compact ? 0 : 1);
        Grid.SetRow(_detail, compact ? 1 : 0);
    }
}
