using System.Windows;
using System.Windows.Controls;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.Docking;

/// <summary>
/// A dock pane on the left, right or bottom edge: a header with the title of the selected tool window, a pin button
/// (auto-hide) and a close button, and the tool windows of that side as tabs.
/// </summary>
public sealed class ToolWindowPane : Border
{
    private readonly DockManager _manager;
    private readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _pin;

    internal ToolWindowPane(DockManager manager, DockSide side)
    {
        _manager = manager;
        Side = side;
        this.Brush(BackgroundProperty, Theme.LayerBackground).Brush(BorderBrushProperty, Theme.Divider);
        BorderThickness = side switch
        {
            DockSide.Left => new Thickness(0, 0, 1, 0),
            DockSide.Right => new Thickness(1, 0, 0, 0),
            _ => new Thickness(0, 1, 0, 0),
        };

        Tabs = new TabControl { TabStripPlacement = Dock.Bottom, BorderThickness = new Thickness(0), Padding = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent };
        Tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, Tabs))
            {
                return;
            }

            UpdateTitle();
            if (SelectedItem is { } item)
            {
                _manager.OnItemActivated(item);
            }
        };

        _pin = HeaderButton("\uE718", () => _manager.TogglePin(Side));
        var close = HeaderButton("\uE711", () => _manager.Close(SelectedItem));
        var header = new DockPanel { Height = 30, LastChildFill = true };
        DockPanel.SetDock(close, System.Windows.Controls.Dock.Right);
        DockPanel.SetDock(_pin, System.Windows.Controls.Dock.Right);
        header.Children.Add(close);
        header.Children.Add(_pin);
        header.Children.Add(_title);

        var root = new DockPanel();
        DockPanel.SetDock(header, System.Windows.Controls.Dock.Top);
        root.Children.Add(header);
        root.Children.Add(Tabs);
        Child = root;
        UpdatePinGlyph();
    }

    public DockSide Side { get; }

    public TabControl Tabs { get; }

    /// <summary>Shown by the user (View menu / Shift+Esc hides it).</summary>
    public bool IsShown { get; set; } = true;

    /// <summary>False = auto-hide: the pane collapses to its edge strip and slides in as a flyout.</summary>
    public bool IsPinned { get; private set; } = true;

    public bool HasItems => Tabs.Items.Count > 0;

    public DockItem? SelectedItem => (Tabs.SelectedItem as TabItem)?.Tag as DockItem;

    public IEnumerable<DockItem> Items => Tabs.Items.OfType<TabItem>().Select(t => t.Tag).OfType<DockItem>();

    public void UpdateTitle() => _title.Text = SelectedItem?.Title ?? string.Empty;

    internal void SetPinned(bool pinned)
    {
        IsPinned = pinned;
        UpdatePinGlyph();
    }

    private void UpdatePinGlyph()
    {
        _pin.Content = Theme.Glyph(IsPinned ? "\uE718" : "\uE77A", 12);
        _pin.ToolTip = IsPinned ? "Auto Hide" : "Dock";
    }

    private static Button HeaderButton(string glyph, Action click)
    {
        var button = new Button
        {
            Content = Theme.Glyph(glyph, 12),
            Width = 28,
            Height = 26,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 2, 2, 2),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Focusable = false,
        };
        button.Click += (_, _) => click();
        return button;
    }
}
