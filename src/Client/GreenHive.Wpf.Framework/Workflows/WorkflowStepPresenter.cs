using System.Globalization;
using System.Text.Json.Nodes;
using GreenHive.NET.Contracts.Api;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Security;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;
using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>
/// Builds the UI of one inbox task: input forms (generated from the InputSchema), choices, reviews, approvals, client-side
/// command steps and AI proposals (values, reasoning, confidence - approve, adjust or reject).
/// </summary>
public static class WorkflowStepPresenter
{
    public static FrameworkElement Build(WorkflowInboxViewModel vm, InboxItem item, BackendConnection backend)
    {
        var panel = new SpacedPanel { Spacing = 12, Padding = new Thickness(16) };
        panel.Children.Add(UiKit.Title($"{item.WorkflowName} - {vm.Text(item.StepTitle)}"));
        if (item.Description is { } description)
        {
            panel.Children.Add(UiKit.Caption(vm.Text(description)));
        }

        panel.Children.Add(UiKit.Caption(string.Join("   ·   ",
            vm.T("workflow:inbox.runAs", ("name", item.RunAsName)),
            vm.T("workflow:inbox.since", ("time", item.Since.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))),
            item.Executor.ToString())));

        if (item.LastError is { } lastError)
        {
            panel.Children.Add(MessageBar.Open(MessageSeverity.Warning, null, vm.Text(lastError)));
        }

        var comment = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
        var commentField = UiKit.Field(vm.T("workflow:inbox.comment"), comment);
        Func<string, Task<Result<IReadOnlyList<LookupItem>>>> lookup = url => backend.GetAsync<IReadOnlyList<LookupItem>>(url);

        // ---- AI proposal waiting for approval
        if (item.Proposal is { } proposal)
        {
            var proposalPanel = new SpacedPanel { Spacing = 8 };
            proposalPanel.Children.Add(UiKit.Label(vm.T("workflow:inbox.proposal")));
            if (item.ProposalReasoning is { } reasoning)
            {
                proposalPanel.Children.Add(UiKit.Caption(reasoning));
            }

            if (item.ProposalConfidence is { } confidence)
            {
                proposalPanel.Children.Add(new ProgressBar { Value = confidence * 100, Maximum = 100, Height = 6 });
                proposalPanel.Children.Add(UiKit.Caption(vm.T("workflow:inbox.confidence", ("value", confidence.ToString("P0", CultureInfo.CurrentCulture)))));
            }

            InputForm? form = null;
            if (item.Input is { } schema)
            {
                form = new InputForm(schema, vm.AppContext.Languages, lookup, proposal);
                proposalPanel.Children.Add(form);
            }
            else
            {
                proposalPanel.Children.Add(Values(proposal));
            }

            proposalPanel.Children.Add(commentField);
            proposalPanel.Children.Add(UiKit.Row(
                UiKit.Button(vm.T("workflow:inbox.approve"), async (_, _) =>
                {
                    var outputs = form?.Validate() is { } v ? (v.IsValid ? v.Match(x => x, _ => proposal) : null) : proposal;
                    if (outputs is not null)
                    {
                        await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Decision = "approve", Outputs = outputs, Comment = comment.Text });
                    }
                }, accent: true, glyph: "\uE73E"),
                UiKit.Button(vm.T("workflow:inbox.reject"), async (_, _) =>
                    await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Decision = "reject", Comment = comment.Text }), glyph: "\uE711")));
            panel.Children.Add(UiKit.Card(proposalPanel));
            panel.Children.Add(VariablesCard(vm, item));
            return Scroll(panel);
        }

        switch (item.StepKind)
        {
            case WorkflowStepKind.Input or WorkflowStepKind.AiTask when item.Input is { } inputSchema:
            {
                var form = new InputForm(inputSchema, vm.AppContext.Languages, lookup, item.Variables);
                panel.Children.Add(UiKit.Card(form));
                panel.Children.Add(UiKit.Row(
                    UiKit.Button(vm.T("workflow:inbox.submit"), async (_, _) =>
                    {
                        var valid = form.Validate();
                        if (valid.TryGetValue(out var outputs))
                        {
                            await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Outputs = outputs });
                        }
                    }, accent: true, glyph: "\uE73E"),
                    HandToAi(vm)));
                break;
            }

            case WorkflowStepKind.AiTask:
            {
                var text = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120 };
                panel.Children.Add(text);
                panel.Children.Add(UiKit.Button(vm.T("workflow:inbox.submit"), async (_, _) =>
                    await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Outputs = new JsonObject { ["summary"] = text.Text } }), accent: true));
                break;
            }

            case WorkflowStepKind.Choice:
            {
                var choices = new SpacedPanel { Spacing = 6 };
                var group = "choice-" + item.StepId;
                foreach (var option in item.Options)
                {
                    choices.Children.Add(new RadioButton { Content = vm.Text(option.Label), Tag = option.Value, GroupName = group });
                }

                panel.Children.Add(UiKit.Card(choices));
                panel.Children.Add(UiKit.Button(vm.T("workflow:inbox.submit"), async (_, _) =>
                {
                    if (choices.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag is string value)
                    {
                        await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Decision = value });
                    }
                }, accent: true));
                break;
            }

            case WorkflowStepKind.Review or WorkflowStepKind.Approval:
            {
                var approval = item.StepKind == WorkflowStepKind.Approval;
                panel.Children.Add(commentField);
                panel.Children.Add(UiKit.Row(
                    UiKit.Button(vm.T(approval ? "workflow:inbox.approve" : "workflow:inbox.confirm"), async (_, _) =>
                        await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Decision = approval ? "approve" : "confirm", Comment = comment.Text }),
                        accent: true, glyph: "\uE73E"),
                    UiKit.Button(vm.T("workflow:inbox.reject"), async (_, _) =>
                        await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Decision = "reject", Comment = comment.Text }), glyph: "\uE711")));
                break;
            }

            case WorkflowStepKind.Command or WorkflowStepKind.ModuleStep:
            {
                var run = UiKit.Button(vm.T("workflow:inbox.runCommand"), async (_, _) =>
                {
                    if (vm.CanRunClientStep)
                    {
                        await vm.RunClientStepAsync();
                    }
                    else
                    {
                        await vm.CompleteAsync(new CompleteStepRequest(item.StepId) { Outputs = new JsonObject() });
                    }
                }, accent: true, glyph: "\uE768");
                panel.Children.Add(run);
                break;
            }
        }

        panel.Children.Add(VariablesCard(vm, item));
        panel.Children.Add(UiKit.Button(vm.T("workflow:inbox.cancelInstance"), async (_, _) => await vm.CancelSelectedAsync(), glyph: "\uE711"));
        return Scroll(panel);
    }

    private static Button HandToAi(WorkflowInboxViewModel vm)
    {
        var button = UiKit.Button(vm.T("workflow:inbox.handToAi"), async (_, _) => await vm.HandToAiAsync(), glyph: "\uE99A");
        button.IsEnabled = vm.Has("ai.run");
        return button;
    }

    private static Border VariablesCard(WorkflowInboxViewModel vm, InboxItem item)
    {
        var column = UiKit.Column(UiKit.Label(vm.T("workflow:inbox.variables")), Values(item.Variables));
        return UiKit.Card(column);
    }

    private static FrameworkElement Values(JsonObject values)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var row = 0;
        foreach (var (key, value) in values)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = new TextBlock { Text = key, Opacity = 0.7, Margin = new Thickness(0, row == 0 ? 0 : 4, 16, 0) };
            var text = UiKit.SelectableText(value is JsonValue v ? v.ToString() : value?.ToJsonString() ?? "-");
            text.Margin = new Thickness(0, row == 0 ? 0 : 4, 0, 0);
            Grid.SetRow(name, row);
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 1);
            grid.Children.Add(name);
            grid.Children.Add(text);
            row++;
        }

        return grid;
    }

    private static ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
}
