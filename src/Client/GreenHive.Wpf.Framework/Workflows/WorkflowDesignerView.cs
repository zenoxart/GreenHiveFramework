using System.Globalization;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>Workflow list (left) and the selected definition with its instances (right).</summary>
public sealed class WorkflowDesignerView : UserControl
{
    private readonly WorkflowDesignerViewModel _vm;
    private readonly DataList<WorkflowSummary> _list;
    private readonly SpacedPanel _detail = new() { Spacing = 12, Padding = new Thickness(16) };
    private readonly Button _record;

    public WorkflowDesignerView(WorkflowDesignerViewModel vm)
    {
        _vm = vm;
        _list = new DataList<WorkflowSummary>(Row, w => w.Id, vm.T("workflow:list.empty"));
        _list.SelectionChanged += async (_, item) =>
        {
            if (item is not null && item.Id != vm.Selected?.Id)
            {
                await vm.SelectAsync(item.Id);
            }
        };

        _record = UiKit.Button(vm.T("workflow:list.record"), async (_, _) => await ToggleRecordingAsync(), glyph: "\uE7C8");
        var recorder = vm.Workflows.Recorder;
        recorder.Changed += (_, _) => UpdateRecordButton();
        var toolbar = UiKit.Row(
            UiKit.Button(vm.T("workflow:inbox.refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"),
            UiKit.Button(vm.T("workflow:list.new"), async (_, _) => await EditAsync(vm.NewDefinition()), glyph: "\uE710"),
            _record,
            UiKit.Busy(vm));
        toolbar.Margin = new Thickness(12, 8, 12, 8);
        foreach (var child in toolbar.Children.OfType<Button>().Skip(1))
        {
            child.IsEnabled = vm.CanEdit;
        }

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 240 });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        var detailScroll = new ScrollViewer { Content = _detail };
        Grid.SetColumn(detailScroll, 1);
        layout.Children.Add(_list.View);
        layout.Children.Add(detailScroll);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var messages = UiKit.Messages(vm);
        messages.Margin = new Thickness(12, 0, 12, 0);
        Grid.SetRow(messages, 1);
        Grid.SetRow(layout, 2);
        root.Children.Add(toolbar);
        root.Children.Add(messages);
        root.Children.Add(layout);
        Content = root;

        UiKit.Watch(vm, nameof(WorkflowDesignerViewModel.All), () => _list.SetItems(vm.All));
        UiKit.Watch(vm, nameof(WorkflowDesignerViewModel.Selected), ShowDetail);
        UiKit.Watch(vm, nameof(WorkflowDesignerViewModel.Instances), ShowDetail);
        UpdateRecordButton();
    }

    private FrameworkElement Row(WorkflowSummary w)
    {
        var column = UiKit.Column(
            UiKit.Label(w.Name),
            UiKit.Caption($"v{w.Version} · {w.Status} · {string.Join(", ", w.TriggerKinds)} · AI {w.AiAutonomy}" +
                          (w.RunningInstances > 0 ? $" · {w.RunningInstances} running" : string.Empty)));
        column.Spacing = 2;
        column.Padding = new Thickness(0, 6, 0, 6);
        if (w.Status is WorkflowStatus.NeedsReview)
        {
            var badge = GreenHive.Wpf.Framework.Theming.Theme.Glyph("\uEA39", 14);
            badge.Foreground = GreenHive.Wpf.Framework.Theming.Theme.Error;
            badge.HorizontalAlignment = HorizontalAlignment.Left;
            column.Children.Add(badge);
        }

        return column;
    }

    private void ShowDetail()
    {
        _detail.Children.Clear();
        if (_vm.Selected is not { } d)
        {
            return;
        }

        _detail.Children.Add(UiKit.Title(d.Name));
        if (d.Description is { } description)
        {
            _detail.Children.Add(UiKit.Caption(description));
        }

        _detail.Children.Add(UiKit.Caption($"v{d.Version} · {d.Status} · {d.Origin}{(d.OwningModule is null ? string.Empty : " · " + d.OwningModule)} · " +
                                           _vm.T("workflow:list.policy", ("autonomy", d.AiPolicy.Autonomy))));
        if (d.Status == WorkflowStatus.NeedsReview)
        {
            _detail.Children.Add(MessageBar.Open(MessageSeverity.Warning, WorkflowStatus.NeedsReview.ToString(),
                string.Join(" ", d.Triggers.Where(t => t.Suspended).Select(t => t.SuspendedReason))));
        }

        var actions = UiKit.Row(
            UiKit.Button(_vm.T("workflow:list.start"), async (_, _) => await _vm.StartAsync(WorkflowExecutor.Human), accent: true, glyph: "\uE768"),
            UiKit.Button(_vm.T("workflow:list.startAi"), async (_, _) => await _vm.StartAsync(WorkflowExecutor.Ai), glyph: "\uE99A"),
            UiKit.Button(_vm.T(d.Status == WorkflowStatus.Active ? "workflow:list.disable" : "workflow:list.enable"),
                async (_, _) => await _vm.SetStatusAsync(d.Status == WorkflowStatus.Active ? WorkflowStatus.Disabled : WorkflowStatus.Active)),
            UiKit.Button(_vm.T("workflow:list.edit"), async (_, _) => await EditAsync(d), glyph: "\uE70F"));
        actions.Children[1].Visibility = d.AiPolicy.Autonomy == AiAutonomy.Off ? Visibility.Collapsed : Visibility.Visible;
        ((Button)actions.Children[2]).IsEnabled = _vm.CanEdit;
        ((Button)actions.Children[3]).IsEnabled = _vm.CanEdit;
        _detail.Children.Add(actions);

        _detail.Children.Add(Section(_vm.T("workflow:list.steps"), d.Steps.Select(s =>
            $"{s.Id}  ·  {s.Kind}  ·  {_vm.Text(s.Title)}{((s.StepTypeId ?? s.CommandId) is { } x ? "  ·  " + x : string.Empty)}{(s.Critical ? "  ·  critical" : string.Empty)}  →  " +
            string.Join(" | ", d.Transitions.Where(t => t.From == s.Id).OrderBy(t => t.Order).Select(t => t.Condition is null ? t.To : $"{t.To} if {t.Condition}")))));
        _detail.Children.Add(Section(_vm.T("workflow:list.triggers"), d.Triggers.Select(t =>
            $"{t.Kind} {t.EventType ?? t.CommandId ?? t.Schedule ?? string.Empty} · {t.Executor}{(t.Filter is null ? string.Empty : " · " + t.Filter)}{(t.Suspended ? " · SUSPENDED" : string.Empty)}")));
        _detail.Children.Add(Section(_vm.T("workflow:list.dependencies"), d.Dependencies.Select(x => $"{x.Kind}: {x.Id} {x.Version}  ({string.Join(", ", x.UsedBySteps)})")));
        _detail.Children.Add(Section(_vm.T("workflow:list.instances"), _vm.Instances.Take(20).Select(i =>
            $"{i.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}  ·  {i.State}  ·  {i.CurrentStepId ?? "-"}  ·  {i.RunAsName}  ·  {i.Executor}{(i.LastError is null ? string.Empty : "  ·  " + _vm.Text(i.LastError))}")));
    }

    private static Border Section(string title, IEnumerable<string> lines)
    {
        var column = new SpacedPanel { Spacing = 4 };
        column.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        foreach (var line in lines)
        {
            column.Children.Add(UiKit.SelectableText(line, monospace: true));
        }

        return UiKit.Card(column, 12);
    }

    private async Task EditAsync(WorkflowDefinition definition)
    {
        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            Text = _vm.ToJson(definition),
            MinWidth = 640,
            MinHeight = 420,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        editor.MaxHeight = 520;
        var publish = new CheckBox { Content = _vm.T("workflow:list.publish"), IsChecked = true };
        var errors = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = GreenHive.Wpf.Framework.Theming.Theme.Error };
        await UiKit.DialogAsync(this, definition.Name, UiKit.Column(editor, publish, errors), _vm.T("shell:dialog.save"), _vm.T("shell:dialog.cancel"), async () =>
        {
            var parsed = _vm.Parse(editor.Text);
            if (!parsed.TryGetValue(out var valid, out var error))
            {
                errors.Text = _vm.T("workflow:list.invalid") + " " + _vm.Format(error);
                return false;
            }

            return await _vm.SaveAsync(valid, publish.IsChecked == true);
        });
    }

    private async Task ToggleRecordingAsync()
    {
        var recorder = _vm.Workflows.Recorder;
        if (recorder.IsRecording)
        {
            if (recorder.Stop() is { } draft)
            {
                await EditAsync(draft);
            }

            return;
        }

        var name = new TextBox { MinWidth = 320 };
        if (await UiKit.DialogAsync(this, _vm.T("workflow:list.record"), UiKit.Field(_vm.T("workflow:list.recordName"), name), _vm.T("workflow:list.record"), _vm.T("shell:dialog.cancel")))
        {
            recorder.Start(name.Text);
        }
    }

    private void UpdateRecordButton()
    {
        var recorder = _vm.Workflows.Recorder;
        _record.Content = recorder.IsRecording ? _vm.T("workflow:list.stopRecording", ("count", recorder.StepCount)) : _vm.T("workflow:list.record");
    }
}
