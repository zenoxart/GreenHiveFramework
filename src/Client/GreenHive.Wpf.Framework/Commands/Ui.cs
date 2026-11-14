using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GreenHive.Wpf.Framework.Theming;
using GreenHive.Wpf.Framework.XamlMetadata;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>
/// Attached properties that make icons, texts and tooltips one-liners in XAML or code
/// (xmlns:gh="clr-namespace:GreenHive.Wpf.Framework.Commands;assembly=GreenHive.Wpf.Framework"):
/// <list type="bullet">
/// <item>gh:Ui.Command="shell.palette" - command, icon, label, tooltip with shortcut, accessibility text.</item>
/// <item>gh:Ui.Icon="save", gh:Ui.Text="shell:common.save", gh:Ui.ToolTip="shell:...".</item>
/// <item>gh:Ui.ShowLabel="False" for icon-only buttons.</item>
/// </list>
/// Values update live when the language or a shortcut changes.
/// </summary>
public static class Ui
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(string), typeof(Ui), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached("Icon", typeof(string), typeof(Ui), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Ui), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty ToolTipProperty =
        DependencyProperty.RegisterAttached("ToolTip", typeof(string), typeof(Ui), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty ShowLabelProperty =
        DependencyProperty.RegisterAttached("ShowLabel", typeof(bool), typeof(Ui), new PropertyMetadata(true, OnChanged));

    private static readonly List<WeakReference<FrameworkElement>> Tracked = [];
    private static RelayCommandPool? _pool;

    /// <summary>The command manager the attached properties use (set by <see cref="Initialize"/>).</summary>
    public static RelayCommandPool? Pool => _pool;

    /// <summary>Connects the attached properties to the command manager. Call once at startup.</summary>
    public static void Initialize(RelayCommandPool pool)
    {
        _pool = pool;
        pool.AppContext.Languages.LanguageChanged += (_, _) => RefreshAll();
        pool.Shortcuts.BindingsChanged += (_, _) => RefreshAll();
    }

    public static string? GetCommand(DependencyObject element) => (string?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, string? value) => element.SetValue(CommandProperty, value);

    public static string? GetIcon(DependencyObject element) => (string?)element.GetValue(IconProperty);

    public static void SetIcon(DependencyObject element, string? value) => element.SetValue(IconProperty, value);

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static string? GetToolTip(DependencyObject element) => (string?)element.GetValue(ToolTipProperty);

    public static void SetToolTip(DependencyObject element, string? value) => element.SetValue(ToolTipProperty, value);

    public static bool GetShowLabel(DependencyObject element) => (bool)element.GetValue(ShowLabelProperty);

    public static void SetShowLabel(DependencyObject element, bool value) => element.SetValue(ShowLabelProperty, value);

    /// <summary>Re-applies all tracked elements (language, shortcuts or command changes).</summary>
    public static void RefreshAll()
    {
        if (_pool is null)
        {
            return;
        }

        _pool.Dispatcher.Run(() =>
        {
            List<FrameworkElement> alive = [];
            lock (Tracked)
            {
                Tracked.RemoveAll(w => !w.TryGetTarget(out _));
                foreach (var reference in Tracked)
                {
                    if (reference.TryGetTarget(out var element))
                    {
                        alive.Add(element);
                    }
                }
            }

            foreach (var element in alive)
            {
                Apply(element);
            }
        });
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        lock (Tracked)
        {
            if (!Tracked.Any(w => w.TryGetTarget(out var t) && ReferenceEquals(t, element)))
            {
                Tracked.Add(new WeakReference<FrameworkElement>(element));
            }
        }

        Apply(element);
    }

    private static void Apply(FrameworkElement element)
    {
        var pool = _pool;
        if (pool is null)
        {
            return;
        }

        var languages = pool.AppContext.Languages;
        var commandId = GetCommand(element);
        var icon = GetIcon(element);
        var textKey = GetText(element);
        var toolTipKey = GetToolTip(element);
        string? label = null;
        string? toolTip = null;
        string? shortcut = null;

        if (!string.IsNullOrEmpty(commandId))
        {
            var command = pool.Get(commandId);
            switch (element)
            {
                case ButtonBase button:
                    button.Command = command;
                    break;
                case MenuItem menuItem:
                    menuItem.Command = command;
                    break;
            }

            label = pool.GetLabel(commandId);
            toolTip = pool.GetToolTipText(commandId);
            shortcut = pool.Shortcuts.GetGestureText(commandId);
            icon ??= pool.GetDescriptor(commandId)?.Icon;
            if (element is MenuItem item)
            {
                item.InputGestureText = shortcut ?? string.Empty;
            }
        }

        if (!string.IsNullOrEmpty(textKey))
        {
            label = languages.GetText(textKey);
        }

        if (!string.IsNullOrEmpty(toolTipKey))
        {
            toolTip = languages.GetText(toolTipKey);
        }

        ApplyLabelAndIcon(pool, element, label, icon, GetShowLabel(element));

        var isTextOnly = element is TextBlock;
        if (!isTextOnly && element is not MenuItem && (toolTip is not null || shortcut is not null || (label is not null && !GetShowLabel(element))))
        {
            element.ToolTip = RichToolTipBuilder.Build(label, shortcut, toolTip);
            ToolTipService.SetShowOnDisabled(element, true);
        }

        if (!isTextOnly && label is not null)
        {
            AutomationProperties.SetName(element, label);
        }

        if (toolTip is not null)
        {
            AutomationProperties.SetHelpText(element, toolTip);
        }

        if (shortcut is not null)
        {
            AutomationProperties.SetAcceleratorKey(element, shortcut);
        }
    }

    private static void ApplyLabelAndIcon(RelayCommandPool pool, FrameworkElement element, string? label, string? icon, bool showLabel)
    {
        var icons = (pool.AppContext as IIconContext)?.Icons;
        FrameworkElement? CreateIcon() => icon is null || icons is null ? null : icons.CreateIcon(icon);

        switch (element)
        {
            case TextBlock textBlock:
                if (label is not null)
                {
                    textBlock.Text = label;
                }

                break;
            case MenuItem menuItem:
                if (label is not null)
                {
                    menuItem.Header = label;
                }

                if (CreateIcon() is { } menuIcon)
                {
                    menuItem.Icon = menuIcon;
                }

                break;
            case ContentControl content when content is ButtonBase:
                var iconElement = CreateIcon();
                var text = showLabel ? label : null;
                if (iconElement is not null && text is not null)
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal };
                    panel.Children.Add(iconElement);
                    panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
                    content.Content = panel;
                }
                else if (iconElement is not null)
                {
                    content.Content = iconElement;
                }
                else if (text is not null)
                {
                    content.Content = text;
                }

                break;
        }
    }
}
