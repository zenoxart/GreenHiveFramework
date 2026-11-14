using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>Small helpers to build views in code (modules use viewMode codeOnly - no compiled XAML, hot-swappable).</summary>
public static class UiKit
{
    /// <summary>Looks up a resource of the application (null when the theme does not define it).</summary>
    public static T? Resource<T>(string key)
        where T : class => Application.Current?.TryFindResource(key) as T;

    public static TextBlock Text(string text, bool wrap = false) =>
        new()
        {
            Text = text,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

    public static TextBlock Title(string text) => Text(text).Title();

    public static TextBlock Caption(string text) => Text(text, wrap: true).Caption();

    public static TextBlock Label(string text) => Text(text).Strong();

    /// <summary>A button bound to a command of the central command manager (icon, text, tooltip, shortcut, enabled state).</summary>
    public static Button CommandButton(string commandId, object? parameter = null, bool showLabel = true)
    {
        var button = new Button { CommandParameter = parameter, Padding = new Thickness(10, 4, 10, 4) };
        Ui.SetShowLabel(button, showLabel);
        Ui.SetCommand(button, commandId);
        return button;
    }

    public static Button Button(string text, RoutedEventHandler click, bool accent = false, string? glyph = null)
    {
        object content = text;
        if (glyph is not null)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(Theme.Glyph(glyph, 14));
            panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            content = panel;
        }

        var button = new Button { Content = content, Padding = new Thickness(10, 4, 10, 4) };
        if (accent)
        {
            button.Styled("AccentButtonStyle");
        }

        button.Click += click;
        return button;
    }

    public static SpacedPanel Row(params UIElement[] children) => Stack(Orientation.Horizontal, 8, children);

    public static SpacedPanel Column(params UIElement[] children) => Stack(Orientation.Vertical, 8, children);

    /// <summary>A stack with spacing between the children.</summary>
    public static SpacedPanel Stack(Orientation orientation, double spacing, params UIElement[] children)
    {
        var panel = new SpacedPanel { Orientation = orientation, Spacing = spacing };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>A link-style button (HyperlinkButton look).</summary>
    public static Button Link(string text, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = text, TextDecorations = TextDecorations.Underline, TextWrapping = TextWrapping.Wrap },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Brush(Control.ForegroundProperty, Theme.Accent);
        button.Click += click;
        return button;
    }

    /// <summary>A drop-down button: clicking opens a menu built by <paramref name="items"/> at that moment.</summary>
    public static Button MenuButton(string text, Func<IEnumerable<(string Text, Action Click)>> items)
    {
        var button = Button(text + "  \uE70D", (_, _) => { });
        button.Content = Row(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }, Theme.Glyph("\uE70D", 10));
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            foreach (var (label, click) in items())
            {
                var entry = new MenuItem { Header = label };
                entry.Click += (_, _) => click();
                menu.Items.Add(entry);
            }

            if (menu.Items.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "-", IsEnabled = false });
            }

            menu.IsOpen = true;
        };
        return button;
    }

    /// <summary>Selectable, read-only text (TextBlock has no text selection in WPF).</summary>
    public static TextBox SelectableText(string text, bool monospace = false) => new()
    {
        Text = text,
        IsReadOnly = true,
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        Padding = new Thickness(0),
        TextWrapping = TextWrapping.Wrap,
        FontFamily = monospace ? new FontFamily("Cascadia Mono, Consolas") : SystemFonts.MessageFontFamily,
        FontSize = monospace ? 12 : SystemFonts.MessageFontSize,
    };

    /// <summary>A rounded card on the layer background.</summary>
    public static Border Card(UIElement content, double padding = 16) => new Border
    {
        Child = content,
        Padding = new Thickness(padding),
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(1),
    }.Brush(Border.BackgroundProperty, Theme.CardBackground).Brush(Border.BorderBrushProperty, Theme.CardStroke);

    /// <summary>A labelled field (label above the control).</summary>
    public static StackPanel Field(string label, FrameworkElement control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(control);
        return panel;
    }

    /// <summary>A message bar (Fluent InfoBar look) with severity colour and a close button.</summary>
    public static MessageBar Message(MessageSeverity severity) => new(severity);

    /// <summary>Message bars that show the ViewModel's ErrorText (and InfoText) while it is set.</summary>
    public static SpacedPanel Messages(ResultViewModel viewModel)
    {
        var error = new MessageBar(MessageSeverity.Error);
        var info = new MessageBar(MessageSeverity.Success);
        Watch(viewModel, nameof(ResultViewModel.ErrorText), () => error.Show(viewModel.ErrorText));
        Watch(viewModel, nameof(ResultViewModel.InfoText), () => info.Show(viewModel.InfoText));
        return Column(error, info);
    }

    /// <summary>A small busy indicator that is active while the ViewModel is busy.</summary>
    public static ProgressBar Busy(ViewModelAppObject viewModel)
    {
        var bar = new ProgressBar { Width = 60, Height = 4, IsIndeterminate = true, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        Watch(viewModel, nameof(ViewModelAppObject.IsBusy), () => bar.Visibility = viewModel.IsBusy ? Visibility.Visible : Visibility.Collapsed);
        return bar;
    }

    /// <summary>Runs <paramref name="update"/> now and whenever the property changes (null = any property).</summary>
    public static void Watch(INotifyPropertyChanged source, string? property, Action update)
    {
        update();
        source.PropertyChanged += (_, e) =>
        {
            if (property is null || string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == property)
            {
                update();
            }
        };
    }

    public static Task<bool> ConfirmAsync(DependencyObject owner, string title, string message, string confirm, string cancel) =>
        DialogAsync(owner, title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 }, confirm, cancel);

    /// <summary>Shows content in a modal dialog; <paramref name="accept"/> may refuse to close (e.g. validation failed).</summary>
    public static Task<bool> DialogAsync(DependencyObject owner, string title, UIElement content, string primary, string close, Func<Task<bool>>? accept = null)
    {
        var completion = new TaskCompletionSource<bool>();
        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            MinWidth = 360,
            MaxHeight = 720,
            Owner = Window.GetWindow(owner) ?? Application.Current?.MainWindow,
        };
        window.Brush(Control.BackgroundProperty, Theme.SolidBackground);

        var ok = Button(primary, (_, _) => { }, accent: true);
        ok.IsDefault = true;
        ok.MinWidth = 96;
        var cancel = new Button { Content = close, IsCancel = true, MinWidth = 96, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        ok.Click += async (_, _) =>
        {
            ok.IsEnabled = false;
            try
            {
                if (accept is null || await accept())
                {
                    window.DialogResult = true;
                }
            }
            finally
            {
                ok.IsEnabled = true;
            }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var layout = new DockPanel { Margin = new Thickness(20) };
        var heading = Title(title);
        heading.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(heading, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(heading);
        layout.Children.Add(buttons);
        layout.Children.Add(new ScrollViewer { Content = content, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        window.Content = layout;

        window.Dispatcher.BeginInvoke(() => completion.TrySetResult(window.ShowDialog() == true));
        return completion.Task;
    }

    public static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)} {currency}";
}

/// <summary>Severity of a <see cref="MessageBar"/>.</summary>
public enum MessageSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>An InfoBar-like message: coloured stripe, icon, text and close button. Hidden while it has no text.</summary>
public sealed class MessageBar : Border
{
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly ContentControl _action = new() { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button _close;

    public MessageBar(MessageSeverity severity)
    {
        var (brush, glyph) = severity switch
        {
            MessageSeverity.Success => (Theme.Success, "\uE930"),
            MessageSeverity.Warning => (Theme.Warning, "\uE7BA"),
            MessageSeverity.Error => (Theme.Error, "\uEA39"),
            _ => (Theme.Info, "\uE946"),
        };

        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        BorderBrush = brush;
        Padding = new Thickness(12, 8, 8, 8);
        Background = new SolidColorBrush(Color.FromArgb(0x1A, ((SolidColorBrush)brush).Color.R, ((SolidColorBrush)brush).Color.G, ((SolidColorBrush)brush).Color.B));
        Visibility = Visibility.Collapsed;

        var icon = Theme.Glyph(glyph, 16);
        icon.Foreground = brush;
        icon.Margin = new Thickness(0, 0, 10, 0);
        _close = new Button { Content = Theme.Glyph("\uE711", 10), Width = 26, Height = 24, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Top };
        _close.Click += (_, _) => Visibility = Visibility.Collapsed;
        var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(_title);
        body.Children.Add(_text);
        body.Children.Add(_action);
        var layout = new DockPanel();
        icon.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(_close, Dock.Right);
        layout.Children.Add(icon);
        layout.Children.Add(_close);
        layout.Children.Add(body);
        Child = layout;
    }

    /// <summary>Bold title above the message.</summary>
    public string? Title
    {
        get => _title.Text;
        set
        {
            _title.Text = value ?? string.Empty;
            _title.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>Optional action (e.g. a link) below the message.</summary>
    public UIElement? Action
    {
        get => _action.Content as UIElement;
        set
        {
            _action.Content = value;
            _action.Visibility = value is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public bool IsClosable
    {
        get => _close.Visibility == Visibility.Visible;
        set => _close.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Creates a bar that is shown right away (static messages).</summary>
    public static MessageBar Open(MessageSeverity severity, string? title, string? message, bool closable = false)
    {
        var bar = new MessageBar(severity) { Title = title, IsClosable = closable };
        bar.Show(message ?? string.Empty);
        bar.Visibility = Visibility.Visible;
        return bar;
    }

    public string? Message => _text.Text;

    /// <summary>Shows the text, or hides the bar when it is null or empty.</summary>
    public void Show(string? message)
    {
        _text.Text = message ?? string.Empty;
        _text.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        Visibility = string.IsNullOrEmpty(message) && string.IsNullOrEmpty(_title.Text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
