using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.Docking;

/// <summary>
/// A floating (detached) view in its own window. The ViewModel is the same as when the view was docked, so no state is
/// lost; the window's monitor, position and size are restored from AppData by the WindowManager.
/// </summary>
public sealed class FloatingDockWindow : Window
{
    private readonly DockManager _manager;

    internal FloatingDockWindow(DockManager manager, DockItem item)
    {
        _manager = manager;
        Item = item;
        Width = Math.Max(720, item.Descriptor.MinWidth);
        Height = Math.Max(520, item.Descriptor.MinHeight);
        MinWidth = Math.Max(item.Descriptor.MinWidth, 240);
        MinHeight = Math.Max(item.Descriptor.MinHeight, 160);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        this.Brush(BackgroundProperty, Theme.SolidBackground);

        var dock = new Button
        {
            Content = Theme.Glyph("\uE90C", 12),
            ToolTip = "Dock (Ctrl+Shift+A)",
            Width = 30,
            Height = 26,
            Padding = new Thickness(0),
            Margin = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Right,
            Focusable = false,
        };
        dock.Click += (_, _) => _manager.Dock(Item);

        var root = new DockPanel();
        DockPanel.SetDock(dock, System.Windows.Controls.Dock.Top);
        root.Children.Add(dock);
        root.Children.Add(DockHost.AsElement(item.View));
        Content = root;
        Activated += (_, _) => _manager.OnItemActivated(Item);
        Closed += (_, _) => _manager.OnFloatingClosed(this);
        UpdateTitle();
    }

    public DockItem Item { get; }

    internal bool IsClosingSilently { get; private set; }

    /// <summary>Shows the window if needed and brings it to the front.</summary>
    public void ActivateWindow()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    internal void CloseSilently()
    {
        IsClosingSilently = true;
        Content = null;
        Close();
    }

    internal void UpdateTitle() => Title = Item.Title;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsClosingSilently && !Item.ViewModel.CanClose())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
