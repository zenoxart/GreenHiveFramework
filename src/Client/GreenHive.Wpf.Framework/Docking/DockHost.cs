using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.Docking;

/// <summary>
/// The dock layout of the main window (Visual Studio style): a document well with tabs in the centre, tool window panes
/// on the left, right and bottom with splitters, auto-hide strips on the edges, and dock guides while a tab is dragged.
/// </summary>
public sealed class DockHost : Grid
{
    private const double StripSize = 26;
    private readonly DockManager _manager;
    private readonly ColumnDefinition _leftColumn = new() { Width = new GridLength(280) };
    private readonly ColumnDefinition _rightColumn = new() { Width = new GridLength(300) };
    private readonly RowDefinition _bottomRow = new() { Height = new GridLength(220) };
    private readonly GridSplitter _leftSplitter;
    private readonly GridSplitter _rightSplitter;
    private readonly GridSplitter _bottomSplitter;
    private readonly StackPanel _leftStrip = new() { Orientation = Orientation.Vertical };
    private readonly StackPanel _rightStrip = new() { Orientation = Orientation.Vertical };
    private readonly StackPanel _bottomStrip = new() { Orientation = Orientation.Horizontal };
    private readonly Canvas _overlay = new() { IsHitTestVisible = true, Background = null };
    private readonly Border _empty;
    private readonly HashSet<DockSide> _compactHidden = [];
    private ToolWindowPane? _flyoutPane;

    internal DockHost(DockManager manager)
    {
        _manager = manager;
        Documents = new TabControl { BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        Documents.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, Documents) && ActiveDocument is { } item)
            {
                _manager.OnItemActivated(item);
            }
        };
        Left = new ToolWindowPane(manager, DockSide.Left);
        Right = new ToolWindowPane(manager, DockSide.Right);
        Bottom = new ToolWindowPane(manager, DockSide.Bottom);
        _empty = new Border { Child = new TextBlock { Text = "Ctrl+Shift+P · Ctrl+Tab", Opacity = 0.45, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0 left strip
        ColumnDefinitions.Add(_leftColumn);                                     // 1 left pane
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2 splitter
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 }); // 3 documents
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 4 splitter
        ColumnDefinitions.Add(_rightColumn);                                    // 5 right pane
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 6 right strip
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 }); // 0 documents
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // 1 splitter
        RowDefinitions.Add(_bottomRow);                                          // 2 bottom pane
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // 3 bottom strip

        Place(_leftStrip, 0, 0, 3);
        Place(Left, 1, 0, 3);
        _leftSplitter = Splitter(vertical: true);
        Place(_leftSplitter, 2, 0, 3);
        Place(_empty, 3, 0);
        Place(Documents, 3, 0);
        _bottomSplitter = Splitter(vertical: false);
        Place(_bottomSplitter, 3, 1);
        Place(Bottom, 3, 2);
        _rightSplitter = Splitter(vertical: true);
        Place(_rightSplitter, 4, 0, 3);
        Place(Right, 5, 0, 3);
        Place(_rightStrip, 6, 0, 3);
        Place(_bottomStrip, 0, 3, 1, 7);
        Place(_overlay, 0, 0, 4, 7);
        Panel.SetZIndex(_overlay, 100);
        _overlay.Visibility = Visibility.Collapsed;

        PreviewMouseDown += (_, e) =>
        {
            if (_flyoutPane is { } pane && !pane.IsMouseOver && !IsInStrip(e.OriginalSource as DependencyObject))
            {
                CloseFlyout();
            }
        };
        SizeChanged += (_, _) => LayoutFlyout();
        UpdateLayoutState();
    }

    public TabControl Documents { get; }

    public ToolWindowPane Left { get; }

    public ToolWindowPane Right { get; }

    public ToolWindowPane Bottom { get; }

    public double LeftWidth
    {
        get => _leftColumn.Width.IsAbsolute && _leftColumn.Width.Value > 0 ? _leftColumn.Width.Value : _savedLeft;
        set => _savedLeft = value;
    }

    public double RightWidth
    {
        get => _rightColumn.Width.IsAbsolute && _rightColumn.Width.Value > 0 ? _rightColumn.Width.Value : _savedRight;
        set => _savedRight = value;
    }

    public double BottomHeight
    {
        get => _bottomRow.Height.IsAbsolute && _bottomRow.Height.Value > 0 ? _bottomRow.Height.Value : _savedBottom;
        set => _savedBottom = value;
    }

    public DockItem? ActiveDocument => (Documents.SelectedItem as TabItem)?.Tag as DockItem;

    private double _savedLeft = 280;
    private double _savedRight = 300;
    private double _savedBottom = 220;

    /// <summary>Wraps a view object (normally a FrameworkElement) for use as content.</summary>
    public static FrameworkElement AsElement(object? view) => view switch
    {
        FrameworkElement element => element,
        null => new Grid(),
        _ => new ContentControl { Content = view },
    };

    public ToolWindowPane? PaneFor(DockSide side) => side switch
    {
        DockSide.Left => Left,
        DockSide.Right => Right,
        DockSide.Bottom => Bottom,
        _ => null,
    };

    /// <summary>Open items of a side in tab order.</summary>
    public IEnumerable<DockItem> ItemsIn(DockSide side) => side == DockSide.DocumentWell
        ? Documents.Items.OfType<TabItem>().Select(t => t.Tag).OfType<DockItem>()
        : PaneFor(side)?.Items ?? [];

    public bool IsPaneVisible(DockSide side) => PaneFor(side) is { } pane && pane.HasItems && pane.IsShown && pane.IsPinned && !_compactHidden.Contains(side);

    internal void ApplySizeClass(SizeClass sizeClass)
    {
        _compactHidden.Clear();
        if (sizeClass is SizeClass.Compact or SizeClass.Medium)
        {
            // Not saved: the user's layout comes back when the window is wide again.
            _compactHidden.Add(DockSide.Left);
            _compactHidden.Add(DockSide.Right);
            if (sizeClass == SizeClass.Compact)
            {
                _compactHidden.Add(DockSide.Bottom);
            }
        }

        UpdateLayoutState();
    }

    internal void Add(DockItem item, DockSide side, bool select)
    {
        var tab = CreateTab(item);
        item.Tab = tab;
        var tabs = side == DockSide.DocumentWell ? Documents : PaneFor(side)!.Tabs;
        tabs.Items.Add(tab);
        if (select || tabs.Items.Count == 1)
        {
            tabs.SelectedItem = tab;
        }

        if (PaneFor(side) is { } pane)
        {
            pane.IsShown = true;
            pane.UpdateTitle();
        }

        UpdateLayoutState();
    }

    internal void Remove(DockItem item)
    {
        if (item.Tab is TabItem tab && ItemsControl.ItemsControlFromItemContainer(tab) is TabControl tabs)
        {
            tab.Content = null;
            tabs.Items.Remove(tab);
        }
        else
        {
            foreach (var control in new[] { Documents, Left.Tabs, Right.Tabs, Bottom.Tabs })
            {
                if (control.Items.OfType<TabItem>().FirstOrDefault(t => ReferenceEquals(t.Tag, item)) is { } found)
                {
                    found.Content = null;
                    control.Items.Remove(found);
                }
            }
        }

        item.Tab = null;
        item.View = null;
        Left.UpdateTitle();
        Right.UpdateTitle();
        Bottom.UpdateTitle();
        UpdateLayoutState();
    }

    internal void Select(DockItem item)
    {
        if (item.Tab is not TabItem tab)
        {
            return;
        }

        tab.IsSelected = true;
        if (PaneFor(item.Location) is { } pane)
        {
            pane.IsShown = true;
            if (!pane.IsPinned || _compactHidden.Contains(item.Location))
            {
                OpenFlyout(pane);
            }

            UpdateLayoutState();
        }

        if (item.View is UIElement view)
        {
            view.Dispatcher.BeginInvoke(() => view.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    internal void UpdateTitle(DockItem item)
    {
        if (item.Tab is TabItem tab && tab.Header is FrameworkElement header && header.FindName("Title") is TextBlock title)
        {
            title.Text = item.Title;
        }
        else if (item.Tab is TabItem plain)
        {
            plain.Header = CreateHeader(item);
        }

        PaneFor(item.Location)?.UpdateTitle();
        UpdateStrips();
    }

    internal void CycleDocument(int step)
    {
        var count = Documents.Items.Count;
        if (count == 0)
        {
            return;
        }

        var index = (Documents.SelectedIndex + step + count) % count;
        Documents.SelectedIndex = index;
    }

    internal void TogglePane(DockSide side)
    {
        if (PaneFor(side) is not { } pane)
        {
            return;
        }

        if (_flyoutPane == pane)
        {
            CloseFlyout();
            return;
        }

        pane.IsShown = !pane.IsShown || !pane.IsPinned;
        if (pane.IsShown && !pane.IsPinned)
        {
            OpenFlyout(pane);
        }

        UpdateLayoutState();
    }

    internal void TogglePin(DockSide side)
    {
        if (PaneFor(side) is not { } pane)
        {
            return;
        }

        CloseFlyout();
        pane.SetPinned(!pane.IsPinned);
        pane.IsShown = true;
        UpdateLayoutState();
    }

    internal void ApplyState(DockLayoutState state)
    {
        _savedLeft = Clamp(state.LeftWidth, 120, 900);
        _savedRight = Clamp(state.RightWidth, 120, 900);
        _savedBottom = Clamp(state.BottomHeight, 80, 700);
        Left.IsShown = state.LeftShown;
        Right.IsShown = state.RightShown;
        Bottom.IsShown = state.BottomShown;
        Left.SetPinned(state.LeftPinned);
        Right.SetPinned(state.RightPinned);
        Bottom.SetPinned(state.BottomPinned);
        UpdateLayoutState();
    }

    internal void ResetSizes()
    {
        _savedLeft = 280;
        _savedRight = 300;
        _savedBottom = 220;
        foreach (var pane in new[] { Left, Right, Bottom })
        {
            pane.IsShown = true;
            pane.SetPinned(true);
        }

        CloseFlyout();
        UpdateLayoutState();
    }

    // ---------------------------------------------------------------- dock guides (drag a tab)

    /// <summary>Called by the drag service while a tab is dragged: shows the guides and returns the target under the pointer.</summary>
    internal DockSide? ShowGuides(Point position)
    {
        _overlay.Visibility = Visibility.Visible;
        _overlay.Children.Clear();
        var width = ActualWidth;
        var height = ActualHeight;
        DockSide? hit = null;

        void Guide(DockSide side, double x, double y, string glyph)
        {
            var rect = new Rect(x, y, 40, 40);
            var over = rect.Contains(position);
            if (over)
            {
                hit = side;
            }

            var guide = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                CornerRadius = new CornerRadius(6),
                Background = over ? Theme.BrandBrush : Theme.Resolve(Theme.CardBackground),
                BorderBrush = Theme.BrandBrush,
                BorderThickness = new Thickness(1.5),
                Child = Theme.Glyph(glyph, 18),
            };
            Canvas.SetLeft(guide, rect.X);
            Canvas.SetTop(guide, rect.Y);
            _overlay.Children.Add(guide);
        }

        var cx = width / 2 - 20;
        var cy = height / 2 - 20;
        Guide(DockSide.DocumentWell, cx, cy, "\uE8A5");
        Guide(DockSide.Left, cx - 48, cy, "\uE90C");
        Guide(DockSide.Right, cx + 48, cy, "\uE90D");
        Guide(DockSide.Bottom, cx, cy + 48, "\uE90E");
        Guide(DockSide.Left, 8, cy, "\uE90C");
        Guide(DockSide.Right, width - 48, cy, "\uE90D");
        Guide(DockSide.Bottom, cx, height - 48, "\uE90E");

        if (hit is { } target)
        {
            var preview = target switch
            {
                DockSide.Left => new Rect(0, 0, Math.Min(LeftWidth, width / 3), height),
                DockSide.Right => new Rect(width - Math.Min(RightWidth, width / 3), 0, Math.Min(RightWidth, width / 3), height),
                DockSide.Bottom => new Rect(0, height - Math.Min(BottomHeight, height / 3), width, Math.Min(BottomHeight, height / 3)),
                _ => new Rect(width * 0.2, height * 0.1, width * 0.6, height * 0.7),
            };
            var shade = new Border
            {
                Width = preview.Width,
                Height = preview.Height,
                Background = new SolidColorBrush(Color.FromArgb(0x40, Theme.Brand.R, Theme.Brand.G, Theme.Brand.B)),
                BorderBrush = Theme.BrandBrush,
                BorderThickness = new Thickness(2),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(shade, preview.X);
            Canvas.SetTop(shade, preview.Y);
            _overlay.Children.Insert(0, shade);
        }

        return hit;
    }

    internal void HideGuides()
    {
        _overlay.Children.Clear();
        _overlay.Visibility = _flyoutPane is null ? Visibility.Collapsed : Visibility.Visible;
        if (_flyoutPane is { } pane)
        {
            _overlay.Children.Add(pane);
            LayoutFlyout();
        }
    }

    // ---------------------------------------------------------------- tabs

    private TabItem CreateTab(DockItem item) => new()
    {
        Tag = item,
        Header = CreateHeader(item),
        Content = AsElement(item.View),
        ContextMenu = CreateContextMenu(item),
    };

    private FrameworkElement CreateHeader(DockItem item)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
        var nameScope = new NameScope();
        NameScope.SetNameScope(panel, nameScope);
        if (item.Icon is not null && _manager.Icons is { } icons)
        {
            var icon = icons.CreateIcon(item.Icon, 14);
            icon.Margin = new Thickness(0, 0, 6, 0);
            panel.Children.Add(icon);
        }

        var title = new TextBlock { Text = item.Title, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis };
        panel.Children.Add(title);
        nameScope.RegisterName("Title", title);
        if (item.Descriptor.CanClose)
        {
            var close = new Button
            {
                Content = Theme.Glyph("\uE711", 9),
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(6, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Focusable = false,
                ToolTip = "Close (Ctrl+F4)",
            };
            close.Click += (_, _) => _manager.Close(item);
            panel.Children.Add(close);
        }

        panel.MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle && item.Descriptor.CanClose)
            {
                _manager.Close(item);
                e.Handled = true;
            }
        };
        DockDragService.Attach(panel, _manager, this, item);
        return panel;
    }

    private ContextMenu CreateContextMenu(DockItem item)
    {
        var menu = new ContextMenu();
        void Add(string header, Action action, bool enabled = true)
        {
            var entry = new MenuItem { Header = header, IsEnabled = enabled };
            entry.Click += (_, _) => action();
            menu.Items.Add(entry);
        }

        Add("Float", () => _manager.Float(item), item.Descriptor.CanFloat);
        menu.Items.Add(new Separator());
        Add("Dock as document", () => _manager.MoveTo(item, DockSide.DocumentWell));
        Add("Dock left", () => _manager.MoveTo(item, DockSide.Left));
        Add("Dock right", () => _manager.MoveTo(item, DockSide.Right));
        Add("Dock bottom", () => _manager.MoveTo(item, DockSide.Bottom));
        menu.Items.Add(new Separator());
        Add("Close", () => _manager.Close(item), item.Descriptor.CanClose);
        Add("Close all but this", () => _manager.CloseOtherDocuments(item));
        return menu;
    }

    // ---------------------------------------------------------------- auto-hide

    private void OpenFlyout(ToolWindowPane pane)
    {
        if (_flyoutPane == pane)
        {
            return;
        }

        CloseFlyout();
        Detach(pane);
        _flyoutPane = pane;
        _overlay.Visibility = Visibility.Visible;
        _overlay.Children.Add(pane);
        LayoutFlyout();
    }

    private void CloseFlyout()
    {
        if (_flyoutPane is not { } pane)
        {
            return;
        }

        _flyoutPane = null;
        _overlay.Children.Remove(pane);
        _overlay.Visibility = Visibility.Collapsed;
        pane.Width = double.NaN;
        pane.Height = double.NaN;
        Reattach(pane);
        UpdateLayoutState();
    }

    private void LayoutFlyout()
    {
        if (_flyoutPane is not { } pane)
        {
            return;
        }

        var width = Math.Max(ActualWidth - 2 * StripSize, 100);
        var height = Math.Max(ActualHeight - StripSize, 100);
        switch (pane.Side)
        {
            case DockSide.Left:
                pane.Width = Math.Min(_savedLeft, width * 0.8);
                pane.Height = height;
                Canvas.SetLeft(pane, StripSize);
                Canvas.SetTop(pane, 0);
                break;
            case DockSide.Right:
                pane.Width = Math.Min(_savedRight, width * 0.8);
                pane.Height = height;
                Canvas.SetLeft(pane, ActualWidth - StripSize - pane.Width);
                Canvas.SetTop(pane, 0);
                break;
            default:
                pane.Width = width;
                pane.Height = Math.Min(_savedBottom, height * 0.7);
                Canvas.SetLeft(pane, StripSize);
                Canvas.SetTop(pane, ActualHeight - StripSize - pane.Height);
                break;
        }
    }

    private void Detach(ToolWindowPane pane)
    {
        if (Children.Contains(pane))
        {
            Children.Remove(pane);
        }
    }

    private void Reattach(ToolWindowPane pane)
    {
        if (pane.Parent is Panel parent)
        {
            parent.Children.Remove(pane);
        }

        switch (pane.Side)
        {
            case DockSide.Left:
                Place(pane, 1, 0, 3);
                break;
            case DockSide.Right:
                Place(pane, 5, 0, 3);
                break;
            default:
                Place(pane, 3, 2);
                break;
        }
    }

    private bool IsInStrip(DependencyObject? source)
    {
        for (var current = source; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, _leftStrip) || ReferenceEquals(current, _rightStrip) || ReferenceEquals(current, _bottomStrip))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- layout

    private void UpdateLayoutState()
    {
        var leftDocked = IsPaneVisible(DockSide.Left) && _flyoutPane != Left;
        var rightDocked = IsPaneVisible(DockSide.Right) && _flyoutPane != Right;
        var bottomDocked = IsPaneVisible(DockSide.Bottom) && _flyoutPane != Bottom;

        RememberSizes();
        _leftColumn.Width = leftDocked ? new GridLength(_savedLeft) : new GridLength(0);
        _rightColumn.Width = rightDocked ? new GridLength(_savedRight) : new GridLength(0);
        _bottomRow.Height = bottomDocked ? new GridLength(_savedBottom) : new GridLength(0);
        if (_flyoutPane != Left)
        {
            Left.Visibility = leftDocked ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_flyoutPane != Right)
        {
            Right.Visibility = rightDocked ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_flyoutPane != Bottom)
        {
            Bottom.Visibility = bottomDocked ? Visibility.Visible : Visibility.Collapsed;
        }

        _leftSplitter.Visibility = leftDocked ? Visibility.Visible : Visibility.Collapsed;
        _rightSplitter.Visibility = rightDocked ? Visibility.Visible : Visibility.Collapsed;
        _bottomSplitter.Visibility = bottomDocked ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = Documents.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Documents.Visibility = Documents.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        UpdateStrips();
    }

    private void RememberSizes()
    {
        if (_leftColumn.Width.IsAbsolute && _leftColumn.Width.Value > 0)
        {
            _savedLeft = _leftColumn.Width.Value;
        }

        if (_rightColumn.Width.IsAbsolute && _rightColumn.Width.Value > 0)
        {
            _savedRight = _rightColumn.Width.Value;
        }

        if (_bottomRow.Height.IsAbsolute && _bottomRow.Height.Value > 0)
        {
            _savedBottom = _bottomRow.Height.Value;
        }
    }

    private void UpdateStrips()
    {
        FillStrip(_leftStrip, Left, DockSide.Left);
        FillStrip(_rightStrip, Right, DockSide.Right);
        FillStrip(_bottomStrip, Bottom, DockSide.Bottom);
    }

    private void FillStrip(StackPanel strip, ToolWindowPane pane, DockSide side)
    {
        strip.Children.Clear();
        var collapsed = pane.HasItems && pane.IsShown && (!pane.IsPinned || _compactHidden.Contains(side));
        strip.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        if (!collapsed)
        {
            return;
        }

        foreach (var item in pane.Items)
        {
            var text = new TextBlock { Text = item.Title, Margin = new Thickness(2, 6, 2, 6) };
            if (side != DockSide.Bottom)
            {
                text.LayoutTransform = new RotateTransform(90);
            }

            var button = new Button
            {
                Content = text,
                Padding = new Thickness(2),
                Margin = new Thickness(1),
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = Theme.BrandBrush,
                Background = Brushes.Transparent,
                Focusable = false,
                MinWidth = side == DockSide.Bottom ? 0 : StripSize - 4,
                MinHeight = side == DockSide.Bottom ? StripSize - 4 : 0,
            };
            var target = item;
            button.Click += (_, _) =>
            {
                if (target.Tab is TabItem tab)
                {
                    tab.IsSelected = true;
                }

                if (_flyoutPane == pane)
                {
                    CloseFlyout();
                }
                else
                {
                    OpenFlyout(pane);
                }
            };
            strip.Children.Add(button);
        }
    }

    private GridSplitter Splitter(bool vertical)
    {
        var splitter = new GridSplitter
        {
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = vertical ? 4 : double.NaN,
            Height = vertical ? double.NaN : 4,
            Background = Brushes.Transparent,
            ShowsPreview = false,
        };
        splitter.DragCompleted += (_, _) =>
        {
            RememberSizes();
            _manager.OnLayoutChanged();
        };
        return splitter;
    }

    private void Place(UIElement element, int column, int row, int rowSpan = 1, int columnSpan = 1)
    {
        SetColumn(element, column);
        SetRow(element, row);
        SetRowSpan(element, rowSpan);
        SetColumnSpan(element, columnSpan);
        Children.Add(element);
    }

    private static double Clamp(double value, double min, double max) => double.IsNaN(value) || value <= 0 ? min : Math.Clamp(value, min, max);
}
