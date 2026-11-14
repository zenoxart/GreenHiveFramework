using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GreenHive.NET.Core.Common.Tutorial;
using GreenHive.Wpf.Controls.Common.Heatmap;
using GreenHive.Wpf.Controls.Common.Notifications;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Navigation;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.UI.Platform.Shell;

/// <summary>
/// The main window of Green Hive: title area (app name, command palette, profile), menu bar built from the command tree
/// "shell.mainmenu", the Visual Studio style dock layout, the status bar and the overlays (navigation wheel, shortcut cheat
/// sheet, tutorial, in-window toasts). Its placement is saved per monitor in AppData; closing it saves the dock layout.
/// </summary>
public sealed class ShellWindow : Window
{
    private const string MainMenuPlacement = "shell.mainmenu";
    private readonly ClientAppContext _context;
    private readonly Menu _menu = new() { Padding = new Thickness(4, 2, 4, 2), Background = Brushes.Transparent };
    private readonly Grid _wheelOverlay = new() { Visibility = Visibility.Collapsed, Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)) };
    private readonly Grid _cheatSheet = new() { Visibility = Visibility.Collapsed, Background = new SolidColorBrush(Color.FromArgb(0x88, 0, 0, 0)) };
    private readonly Canvas _tutorialOverlay = new() { Visibility = Visibility.Collapsed, Background = Brushes.Transparent };
    private readonly DockPanel _titleArea = new() { LastChildFill = false, Margin = new Thickness(10, 6, 6, 2) };
    private readonly Border _statusBar = new() { Padding = new Thickness(12, 3, 12, 3), BorderThickness = new Thickness(0, 1, 0, 0) };
    private readonly NavigationWheelViewModel _wheelViewModel;
    private readonly NavigationWheelInputHandler _wheelInput;
    private readonly CommandPalette _palette;
    private readonly Button _paletteButton;

    public ShellWindow(ClientAppContext context)
    {
        _context = context;
        ViewModel = context.Produce<ShellViewModel>();
        Width = 1400;
        Height = 860;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.Brush(BackgroundProperty, Theme.SolidBackground);
        AppIcon.Apply(this);

        // ---- title area: app icon + name, palette, profile
        var appName = UiKit.Row(
            AppIcon.CreateImage(20),
            new TextBlock { Text = "Green Hive", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Theme.BrandBrush, VerticalAlignment = VerticalAlignment.Center });
        _paletteButton = new Button
        {
            MinWidth = 360,
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24, 0, 0, 0),
            Content = UiKit.Row(Theme.Glyph("\uE721", 14), new TextBlock { Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center }),
        };
        _paletteButton.Click += (_, _) => FocusPalette();
        _palette = new CommandPalette(context.Commands, _paletteButton);
        DockPanel.SetDock(appName, Dock.Left);
        DockPanel.SetDock(_paletteButton, Dock.Left);
        _titleArea.Children.Add(appName);
        _titleArea.Children.Add(_paletteButton);

        // ---- dock layout and status bar
        var dockHost = context.Dock.CreateHost();
        BuildStatusBar();
        TutorialHelper.SetTutorialKey(_menu, "TopMenuArea");
        TutorialHelper.SetTutorialKey(dockHost.Left, "LeftMenuArea");
        TutorialHelper.SetTutorialKey(dockHost.Documents, "TabArea");
        TutorialHelper.SetTutorialKey(_statusBar, "StatusArea");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_menu, 1);
        Grid.SetRow(dockHost, 2);
        Grid.SetRow(_statusBar, 3);
        root.Children.Add(_titleArea);
        root.Children.Add(_menu);
        root.Children.Add(dockHost);
        root.Children.Add(_statusBar);
        foreach (var overlay in new UIElement[] { _wheelOverlay, _cheatSheet, _tutorialOverlay })
        {
            Grid.SetRowSpan(overlay, 4);
            root.Children.Add(overlay);
        }

        // Classic Green Hive extras: in-window toasts and interaction heatmap tracking.
        var toasts = new NotificationContainer();
        Grid.SetRowSpan(toasts, 4);
        Panel.SetZIndex(toasts, 9999);
        root.Children.Add(toasts);
        HeatmapTrackingBehavior.SetIsTrackingEnabled(root, true);
        HeatmapTrackingBehavior.SetViewName(root, "Shell");
        Content = root;

        BuildMenu();
        context.Commands.TreeChanged += (_, placement) =>
        {
            if (placement is "*" or MainMenuPlacement)
            {
                BuildMenu();
            }
        };

        // ---- responsive layout: the dock follows the size class of the window content
        context.Windows.Responsive.Attach(root, (sizeClass, heightClass) =>
        {
            ViewModel.SizeClass = sizeClass;
            ViewModel.HeightClass = heightClass;
            context.Dock.ApplySizeClass(sizeClass);
            _paletteButton.MinWidth = sizeClass is SizeClass.Compact ? 160 : 360;
        });

        // ---- navigation wheel (Ctrl+Tab)
        _wheelViewModel = context.Produce<NavigationWheelViewModel>();
        _wheelOverlay.Children.Add(new NavigationWheel(_wheelViewModel, context.Icons));
        _wheelInput = new NavigationWheelInputHandler(_wheelViewModel, this,
            visible => _wheelOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed);
        _wheelViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationWheelViewModel.IsOpen))
            {
                context.Windows.InputRouter.IsSuspended = _wheelViewModel.IsOpen;
                if (!_wheelViewModel.IsOpen)
                {
                    _wheelOverlay.Visibility = Visibility.Collapsed;
                }
            }
        };
        _wheelOverlay.MouseLeftButtonUp += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, _wheelOverlay))
            {
                _wheelInput.Cancel();
            }
        };
        Deactivated += (_, _) =>
        {
            if (_wheelViewModel.IsOpen)
            {
                _wheelInput.Cancel();
            }
        };

        _cheatSheet.MouseLeftButtonUp += (_, _) => ToggleCheatSheet();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _cheatSheet.Visibility == Visibility.Visible)
            {
                ToggleCheatSheet();
                e.Handled = true;
            }
        };

        context.Windows.InputRouter.ChordPending += (_, gesture) => ViewModel.SetChordHint(gesture);
        Activated += (_, _) => IsShellActive = true;
        Deactivated += (_, _) => IsShellActive = false;
        UpdateTexts();
        context.Languages.LanguageChanged += (_, _) => UpdateTexts();
        context.Windows.Track(this, "main", 480, 400, isMain: true);
    }

    public ShellViewModel ViewModel { get; }

    /// <summary>True while the window has the focus (desktop toasts are only shown when it has not).</summary>
    public bool IsShellActive { get; private set; }

    /// <summary>Puts the profile of the signed-in user into the right side of the title area.</summary>
    public void AttachProfile(Func<Task> signIn)
    {
        var profile = new ProfileButton(_context, signIn);
        TutorialHelper.SetTutorialKey(profile, "ProfileArea");
        DockPanel.SetDock(profile, Dock.Right);
        _titleArea.Children.Add(profile);
    }

    /// <summary>Runs <paramref name="action"/> once the window content is loaded (dialogs can be owned by it then).</summary>
    public void WhenLoaded(Func<Task> action)
    {
        if (IsLoaded)
        {
            Dispatcher.BeginInvoke(async () => await action());
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Dispatcher.BeginInvoke(async () => await action());
        }

        Loaded += OnLoaded;
    }

    /// <summary>Opens the command palette and shows all commands.</summary>
    public void FocusPalette() => _palette.Open();

    /// <summary>Starts the navigation wheel (Ctrl+Tab or menu).</summary>
    public void BeginWheel(bool reverse) => _wheelInput.Begin(reverse);

    public void ShowAbout()
    {
        var languages = _context.Languages;
        _ = UiKit.DialogAsync(this, languages.GetText("shell:about.title"),
            new TextBlock { Text = languages.GetText("shell:about.text", new Dictionary<string, object?> { ["version"] = _context.Version }), TextWrapping = TextWrapping.Wrap, MaxWidth = 420 },
            languages.GetText("shell:about.close"), languages.GetText("shell:dialog.cancel"));
    }

    /// <summary>Shows or hides the overlay with the shortcuts that work right now (Ctrl+/).</summary>
    public void ToggleCheatSheet()
    {
        if (_cheatSheet.Visibility == Visibility.Visible)
        {
            _cheatSheet.Visibility = Visibility.Collapsed;
            return;
        }

        var pool = _context.Commands;
        var languages = _context.Languages;
        var columns = new WrapPanel { Orientation = Orientation.Vertical, MaxHeight = 520 };
        foreach (var entry in pool.Shortcuts.Effective
                     .Where(e => pool.CanExecute(e.CommandId, null))
                     .GroupBy(e => e.CommandId, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First())
                     .OrderBy(e => pool.GetLabel(e.CommandId), StringComparer.CurrentCultureIgnoreCase))
        {
            columns.Children.Add(DataList<object>.Cells(new double[] { 0, 0 },
                new TextBlock { Text = pool.Shortcuts.Format(entry.Gesture), FontFamily = new FontFamily("Cascadia Mono, Consolas"), Foreground = Theme.BrandBrush, MinWidth = 150 },
                new TextBlock { Text = pool.GetLabel(entry.CommandId), Width = 220, TextTrimming = TextTrimming.CharacterEllipsis }));
        }

        var card = UiKit.Card(UiKit.Column(UiKit.Title(languages.GetText("greenhive:cheatsheet.title")), columns, UiKit.Caption(languages.GetText("greenhive:cheatsheet.hint"))), 24);
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        card.Brush(Border.BackgroundProperty, Theme.SolidBackground);
        _cheatSheet.Children.Clear();
        _cheatSheet.Children.Add(card);
        _cheatSheet.Visibility = Visibility.Visible;
    }

    /// <summary>Starts the classic Green Hive tutorial (highlights menu, navigation, documents and status bar).</summary>
    public void StartTutorial()
    {
        try
        {
            var service = TutorialService.Instance;
            if (!service.HasTutorial("MainWindow"))
            {
                service.LoadTutorialsFromXml(Path.Combine(AppContext.BaseDirectory, "Configuration", "TutorialKonfiguration.xml"));
            }

            _tutorialOverlay.Visibility = Visibility.Visible;
            service.StartTutorial("MainWindow", this, _tutorialOverlay);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Xml.XmlException)
        {
            _context.Log.Write($"Tutorial could not be started: {ex.Message}", LogEntryType.Warning, nameof(ShellWindow));
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_context.Dock.OpenItems.Any(i => !i.ViewModel.CanClose()))
        {
            e.Cancel = true;
            return;
        }

        _context.Hub.Stop();
        _context.HotSwap.Stop();
        _context.Toasts.Unregister();
        _context.Dock.Shutdown();
        _context.Windows.Placements.Save();
        _context.Log.Write("Green Hive ends", LogEntryType.End, nameof(ShellWindow));
        base.OnClosing(e);
    }

    private void BuildMenu() =>
        CommandTreeAdapters.FillMenu(_menu, _context.Commands, _context.Commands.GetTree(MainMenuPlacement));

    private void BuildStatusBar()
    {
        _statusBar.Brush(Border.BackgroundProperty, Theme.LayerBackground).Brush(Border.BorderBrushProperty, Theme.CardStroke);
        TextBlock Caption(double opacity = 1) => new() { FontSize = 12, Opacity = opacity, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var status = Caption();
        var active = Caption();
        active.MaxWidth = 320;
        var notifications = UiKit.Link(string.Empty, (_, _) => ViewModel.OpenNotifications());
        var user = Caption();
        var connection = Caption(0.7);
        var size = Caption(0.7);
        var language = Caption(0.7);

        var grid = new Grid();
        var cells = new FrameworkElement[] { status, active, notifications, user, connection, size, language };
        for (var i = 0; i < cells.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            cells[i].Margin = new Thickness(i == 0 ? 0 : 16, 0, 0, 0);
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        _statusBar.Child = grid;
        UiKit.Watch(ViewModel, null, () =>
        {
            status.Text = ViewModel.StatusText;
            active.Text = ViewModel.ActiveTitle;
            ((TextBlock)notifications.Content).Text = ViewModel.NotificationText;
            notifications.Visibility = string.IsNullOrEmpty(ViewModel.NotificationText) ? Visibility.Collapsed : Visibility.Visible;
            user.Text = ViewModel.UserText;
            connection.Text = ViewModel.ConnectionText;
            size.Text = ViewModel.SizeClassText;
            language.Text = ViewModel.LanguageText;
        });
    }

    private void UpdateTexts()
    {
        Title = _context.Languages.GetText("shell:app.title");
        if (_paletteButton.Content is Panel panel && panel.Children.OfType<TextBlock>().LastOrDefault() is { } hint)
        {
            hint.Text = _context.Languages.GetText("shell:palette.placeholder") + "   (Ctrl+Shift+P)";
        }
    }
}
