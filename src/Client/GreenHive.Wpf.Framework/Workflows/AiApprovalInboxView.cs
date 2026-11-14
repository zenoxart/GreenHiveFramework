using System.Globalization;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.WorkflowContracts.Api;
using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>AI agent tool window.</summary>
public sealed class AiApprovalInboxView : UserControl
{
    private readonly AiApprovalInboxViewModel _vm;
    private readonly SpacedPanel _proposals = new() { Spacing = 8 };
    private readonly SpacedPanel _audit = new() { Spacing = 4 };
    private readonly CheckBox _enabled = new();
    private bool _updating;

    public AiApprovalInboxView(AiApprovalInboxViewModel vm)
    {
        _vm = vm;
        var provider = UiKit.Caption(string.Empty);
        var usage = UiKit.Caption(string.Empty);
        _enabled.Content = vm.T("workflow:ai.enabled");
        _enabled.IsEnabled = vm.CanSwitch;
        RoutedEventHandler toggled = async (_, _) =>
        {
            if (!_updating)
            {
                await vm.SetEnabledAsync(_enabled.IsChecked == true);
            }
        };
        _enabled.Checked += toggled;
        _enabled.Unchecked += toggled;

        var root = new SpacedPanel { Spacing = 12, Padding = new Thickness(12) };
        root.Children.Add(UiKit.Row(UiKit.Button(vm.T("workflow:inbox.refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), UiKit.Busy(vm)));
        root.Children.Add(UiKit.Messages(vm));
        root.Children.Add(UiKit.Card(UiKit.Column(provider, usage, _enabled), 12));
        root.Children.Add(UiKit.Label(vm.T("workflow:ai.proposals")));
        root.Children.Add(_proposals);
        root.Children.Add(UiKit.Label(vm.T("workflow:ai.audit")));
        root.Children.Add(_audit);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        UiKit.Watch(vm, nameof(AiApprovalInboxViewModel.Status), () =>
        {
            if (vm.Status is not { } s)
            {
                return;
            }

            provider.Text = vm.T("workflow:ai.provider", ("provider", s.Provider), ("model", s.Model));
            usage.Text = vm.T("workflow:ai.today", ("runs", s.RunsToday), ("tokens", s.TokensToday));
            _updating = true;
            _enabled.IsChecked = s.Enabled;
            _updating = false;
        });
        UiKit.Watch(vm, nameof(AiApprovalInboxViewModel.Proposals), ShowProposals);
        UiKit.Watch(vm, nameof(AiApprovalInboxViewModel.Audit), () =>
        {
            _audit.Children.Clear();
            foreach (var entry in vm.Audit.Take(40))
            {
                _audit.Children.Add(UiKit.Caption(
                    $"{entry.At.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}  {entry.Decision}  {entry.StepId}" +
                    $"{(entry.Confidence is { } c ? $"  {c:P0}" : string.Empty)}{(entry.PolicyCode is null ? string.Empty : "  " + entry.PolicyCode)}" +
                    $"{(entry.Reasoning is null ? string.Empty : "  - " + entry.Reasoning)}"));
            }
        });
    }

    private void ShowProposals()
    {
        _proposals.Children.Clear();
        foreach (var item in _vm.Proposals)
        {
            var open = UiKit.Link($"{item.WorkflowName} - {_vm.Text(item.StepTitle)}", (_, _) => _vm.Open(item));
            var panel = UiKit.Column(
                open,
                UiKit.Caption(item.ProposalReasoning ?? string.Empty),
                UiKit.Row(
                    UiKit.Button(_vm.T("workflow:inbox.approve"), async (_, _) => await _vm.DecideAsync(item, true), accent: true),
                    UiKit.Button(_vm.T("workflow:inbox.reject"), async (_, _) => await _vm.DecideAsync(item, false))));
            _proposals.Children.Add(UiKit.Card(panel, 10));
        }
    }
}
