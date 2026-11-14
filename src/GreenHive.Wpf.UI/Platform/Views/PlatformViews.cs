using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GreenHive.NET.CommandContracts;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Theming;
using KeyGesture = GreenHive.NET.CommandContracts.KeyGesture;

namespace GreenHive.Wpf.UI.Platform.Views;

/// <summary>Start page: welcome, module tiles, quick actions and tips.</summary>
public sealed class StartView : UserControl
{
    public StartView(StartViewModel vm)
    {
        var languages = vm.AppContext.Languages;
        string T(string key) => languages.GetText("shell:" + key);

        var tiles = new WrapPanel();
        void FillTiles()
        {
            tiles.Children.Clear();
            foreach (var card in vm.Modules)
            {
                var content = UiKit.Column(Theme.Glyph(card.Glyph, 28), UiKit.Label(card.Label), UiKit.Caption(card.Description));
                ((FrameworkElement)content.Children[0]).HorizontalAlignment = HorizontalAlignment.Left;
                var button = new Button
                {
                    Content = content,
                    Width = 220,
                    Height = 130,
                    Margin = new Thickness(0, 0, 12, 12),
                    Padding = new Thickness(14),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Top,
                };
                button.Click += (_, _) => vm.Open(card);
                tiles.Children.Add(button);
            }
        }

        vm.Modules.CollectionChanged += (_, _) => FillTiles();
        FillTiles();

        var title = UiKit.Title(T("start.welcome"));
        title.FontSize = 28;
        var subtitle = UiKit.Caption(T("start.subtitle"));
        var actions = UiKit.Row(
            UiKit.CommandButton("shell.palette"),
            UiKit.CommandButton("shell.navigationwheel.show"),
            UiKit.CommandButton("navigate.workflow.inbox"),
            UiKit.CommandButton("shell.shortcuts.settings"));
        var tips = UiKit.Column(
            UiKit.Caption("• " + T("start.tip.palette")),
            UiKit.Caption("• " + T("start.tip.wheel")),
            UiKit.Caption("• " + T("start.tip.float")),
            UiKit.Caption("• " + T("start.tip.responsive")));
        tips.Spacing = 4;

        var root = new SpacedPanel { Spacing = 16, Padding = new Thickness(32, 28, 32, 28), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(UiKit.Column(title, subtitle));
        root.Children.Add(UiKit.Label(T("start.modules")));
        root.Children.Add(tiles);
        root.Children.Add(UiKit.Label(T("start.quickactions")));
        root.Children.Add(actions);
        root.Children.Add(UiKit.Card(UiKit.Column(UiKit.Label(T("start.tips")), tips)));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}

/// <summary>Module Explorer: the navigation command tree (built-in views, workflows, classic views and every module).</summary>
public sealed class ModuleExplorerView : UserControl
{
    public ModuleExplorerView(ModuleExplorerViewModel vm)
    {
        var tree = new TreeView { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(4) };
        void Fill() => vm.AppContext.Dispatcher?.Run(() => CommandTreeAdapters.FillTreeView(tree, vm.Commands, vm.Tree));
        vm.TreeChanged += (_, _) => Fill();
        Loaded += (_, _) => Fill();
        Content = tree;
    }
}

/// <summary>Notification centre: newest first; a click opens the referenced view.</summary>
public sealed class NotificationsView : UserControl
{
    public NotificationsView(NotificationsViewModel vm)
    {
        var list = new SpacedPanel { Spacing = 6, Padding = new Thickness(8) };
        void Fill()
        {
            list.Children.Clear();
            var items = vm.Items.OrderByDescending(i => i.Time).ToList();
            if (items.Count == 0)
            {
                list.Children.Add(new TextBlock { Text = vm.T("notifications.none"), Opacity = 0.6, Margin = new Thickness(4) });
            }

            foreach (var item in items)
            {
                var stripe = item.Severity switch
                {
                    NotificationSeverity.Success => Theme.Success,
                    NotificationSeverity.Warning => Theme.Warning,
                    NotificationSeverity.Error => Theme.Error,
                    _ => Theme.Info,
                };
                var text = UiKit.Column(
                    new TextBlock { Text = item.Title, FontWeight = item.IsRead ? FontWeights.Normal : FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
                    UiKit.Caption(item.Message ?? string.Empty),
                    UiKit.Caption(item.Time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
                text.Spacing = 2;
                var row = new DockPanel();
                var bar = new Border { Width = 3, Background = stripe, Margin = new Thickness(0, 0, 8, 0) };
                DockPanel.SetDock(bar, Dock.Left);
                row.Children.Add(bar);
                row.Children.Add(text);
                var button = new Button
                {
                    Content = row,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(6),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = item.NavigationTarget is null ? Cursors.Arrow : Cursors.Hand,
                };
                var target = item;
                button.Click += (_, _) => vm.Open(target);
                list.Children.Add(button);
            }
        }

        vm.Changed += (_, _) => vm.AppContext.Dispatcher?.Run(Fill);
        Fill();

        var toolbar = UiKit.Row(
            UiKit.Button(vm.T("notifications.markread"), (_, _) => vm.MarkAllRead(), glyph: "\uE73E"),
            UiKit.Button(vm.T("notifications.clear"), (_, _) => vm.Clear(), glyph: "\uE74D"));
        toolbar.Margin = new Thickness(8, 8, 8, 0);
        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }
}

/// <summary>Output tool window: the application log (newest at the bottom, optional auto-scroll).</summary>
public sealed class OutputView : UserControl
{
    private const int MaxRows = 1000;

    public OutputView(OutputViewModel vm)
    {
        var list = new ListBox
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
        };
        VirtualizingPanel.SetIsVirtualizing(list, true);
        void Append(LogEntry entry)
        {
            var text = new TextBlock { Text = entry.ToString(), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = entry.Details ?? entry.Text };
            if (entry.Type is LogEntryType.Error)
            {
                text.Foreground = Theme.Error;
            }
            else if (entry.Type is LogEntryType.Warning)
            {
                text.Foreground = Theme.Warning;
            }

            list.Items.Add(text);
            while (list.Items.Count > MaxRows)
            {
                list.Items.RemoveAt(0);
            }

            if (vm.AutoScroll)
            {
                list.ScrollIntoView(text);
            }
        }

        foreach (var entry in vm.Entries.ToList().TakeLast(MaxRows))
        {
            Append(entry);
        }

        vm.Entries.CollectionChanged += (_, e) => list.Dispatcher.BeginInvoke(() =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                list.Items.Clear();
            }
            else if (e.NewItems is { } added)
            {
                foreach (var entry in added.OfType<LogEntry>())
                {
                    Append(entry);
                }
            }
        });

        var autoScroll = new CheckBox { Content = vm.AppContext.Languages.GetText("shell:output.autoscroll"), IsChecked = vm.AutoScroll, VerticalAlignment = VerticalAlignment.Center };
        autoScroll.Checked += (_, _) => vm.AutoScroll = true;
        autoScroll.Unchecked += (_, _) => vm.AutoScroll = false;
        var toolbar = UiKit.Row(UiKit.Button(vm.AppContext.Languages.GetText("shell:output.clear"), (_, _) => vm.Clear(), glyph: "\uE74D"), autoScroll);
        toolbar.Margin = new Thickness(6);
        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(list);
        Content = root;
    }
}

/// <summary>Settings: language, keyboard shortcuts (record, reset, conflicts) and data folders.</summary>
public sealed class SettingsView : UserControl
{
    private readonly SettingsViewModel _vm;
    private readonly SpacedPanel _shortcuts = new() { Spacing = 2 };
    private readonly TextBox _search = new() { MinWidth = 260 };

    public SettingsView(SettingsViewModel vm)
    {
        _vm = vm;
        var languages = vm.AppContext.Languages;
        string T(string key) => languages.GetText("shell:" + key);

        var language = new ComboBox { MinWidth = 220, DisplayMemberPath = nameof(LanguageInfo.NativeName) };
        foreach (var info in vm.Languages)
        {
            language.Items.Add(info);
        }

        language.SelectedItem = vm.Languages.FirstOrDefault(l => l.Code == vm.CurrentLanguage.Code);
        language.SelectionChanged += (_, _) =>
        {
            if (language.SelectedItem is LanguageInfo selected)
            {
                vm.SetLanguage(selected);
            }
        };

        _search.TextChanged += (_, _) => FillShortcuts();
        vm.Shortcuts.CollectionChanged += (_, _) => vm.AppContext.Dispatcher?.Run(FillShortcuts);
        PreviewKeyDown += OnPreviewKeyDown;

        var data = UiKit.Column(
            UiKit.Row(UiKit.Caption(T("settings.localdata") + ": " + vm.LocalDataPath), UiKit.Link(T("settings.open"), (_, _) => vm.OpenFolder(vm.LocalDataPath))),
            UiKit.Row(UiKit.Caption(T("settings.roamingdata") + ": " + vm.RoamingDataPath), UiKit.Link(T("settings.open"), (_, _) => vm.OpenFolder(vm.RoamingDataPath))));

        var root = new SpacedPanel { Spacing = 16, Padding = new Thickness(24), MaxWidth = 980, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(UiKit.Title(languages.GetText("shell:view.settings.label")));
        root.Children.Add(UiKit.Card(UiKit.Field(T("settings.language"), language)));
        root.Children.Add(UiKit.Card(UiKit.Column(
            UiKit.Label(T("settings.shortcuts")),
            UiKit.Caption(T("settings.shortcuts.hint")),
            UiKit.Row(_search, UiKit.Button(T("settings.resetall"), (_, _) => vm.ResetAll(), glyph: "\uE72C")),
            _shortcuts)));
        root.Children.Add(UiKit.Card(UiKit.Column(UiKit.Label(T("settings.data")), data)));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        FillShortcuts();
    }

    private void FillShortcuts()
    {
        var languages = _vm.AppContext.Languages;
        string T(string key) => languages.GetText("shell:" + key);
        var filter = _search.Text.Trim();
        _shortcuts.Children.Clear();
        foreach (var row in _vm.Shortcuts.Where(r => filter.Length == 0 || r.Label.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || r.Gesture.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
        {
            var change = UiKit.Button(T("settings.change"), (_, _) => _vm.StartRecording(row.CommandId));
            var reset = UiKit.Button(T("settings.reset"), (_, _) => _vm.Reset(row.CommandId));
            reset.IsEnabled = row.IsChanged;
            var gesture = new TextBlock { Text = row.Gesture, FontFamily = new FontFamily("Cascadia Mono, Consolas"), VerticalAlignment = VerticalAlignment.Center };
            if (row.CommandId == _vm.RecordingCommandId)
            {
                gesture.Foreground = Theme.BrandBrush;
                gesture.FontWeight = FontWeights.SemiBold;
            }

            _shortcuts.Children.Add(DataList<object>.Cells(new double[] { 3, 2, 1, 0, 0 },
                new TextBlock { Text = row.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                gesture,
                UiKit.Caption(row.Source),
                change,
                reset));
        }
    }

    /// <summary>While a gesture is recorded, the next key combination becomes the new shortcut (Esc cancels).</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm.RecordingCommandId is null)
        {
            return;
        }

        var key = ShortcutInputRouter.RealKey(e);
        if (ShortcutInputRouter.IsModifierKey(key))
        {
            return;
        }

        e.Handled = true;
        if (key == Key.Escape)
        {
            _vm.FinishRecording(null);
            return;
        }

        _vm.FinishRecording(new KeyGesture(ShortcutInputRouter.CurrentModifiers(), KeyInterop.VirtualKeyFromKey(key)));
    }
}
