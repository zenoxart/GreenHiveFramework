using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>
/// A selectable list whose rows are built in code (no data templates). Keeps the selection across refreshes by key.
/// </summary>
public sealed class DataList<T>
    where T : class
{
    private readonly Func<T, FrameworkElement> _row;
    private readonly Func<T, object> _key;
    private readonly ListView _list = new() { SelectionMode = SelectionMode.Single, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent };
    private readonly TextBlock _empty;
    private bool _updating;

    public DataList(Func<T, FrameworkElement> row, Func<T, object> key, string emptyText, FrameworkElement? header = null)
    {
        _row = row;
        _key = key;
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _empty = new TextBlock { Text = emptyText, Opacity = 0.6, Margin = new Thickness(12), Visibility = Visibility.Collapsed };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (header is not null)
        {
            header.Margin = new Thickness(12, 4, 12, 4);
            header.Opacity = 0.7;
            grid.Children.Add(header);
        }

        Grid.SetRow(_list, 1);
        Grid.SetRow(_empty, 1);
        grid.Children.Add(_list);
        grid.Children.Add(_empty);
        View = grid;
        _list.SelectionChanged += (_, _) =>
        {
            if (!_updating)
            {
                SelectionChanged?.Invoke(this, SelectedItem);
            }
        };
        _list.MouseDoubleClick += (_, _) =>
        {
            if (SelectedItem is { } item)
            {
                ItemInvoked?.Invoke(this, item);
            }
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter && SelectedItem is { } item)
            {
                ItemInvoked?.Invoke(this, item);
                e.Handled = true;
            }
        };
    }

    public event EventHandler<T?>? SelectionChanged;

    public event EventHandler<T>? ItemInvoked;

    public FrameworkElement View { get; }

    public IReadOnlyList<T> Items { get; private set; } = [];

    public T? SelectedItem => (_list.SelectedItem as ListViewItem)?.Tag as T;

    public void SetItems(IEnumerable<T> items)
    {
        var selectedKey = SelectedItem is { } selected ? _key(selected) : null;
        _updating = true;
        try
        {
            Items = items.ToList();
            _list.Items.Clear();
            ListViewItem? reselect = null;
            foreach (var item in Items)
            {
                var container = new ListViewItem { Content = _row(item), Tag = item, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 6, 8, 6) };
                _list.Items.Add(container);
                if (selectedKey is not null && Equals(_key(item), selectedKey))
                {
                    reselect = container;
                }
            }

            _list.SelectedItem = reselect;
            _empty.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _updating = false;
        }

        SelectionChanged?.Invoke(this, SelectedItem);
    }

    public void Select(Func<T, bool> predicate)
    {
        foreach (var container in _list.Items.OfType<ListViewItem>())
        {
            if (container.Tag is T item && predicate(item))
            {
                _list.SelectedItem = container;
                container.BringIntoView();
                return;
            }
        }
    }

    /// <summary>A row grid with the given column widths (star values; 0 = auto).</summary>
    public static Grid RowGrid(params double[] widths)
    {
        var grid = new Grid();
        foreach (var width in widths)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width <= 0 ? GridLength.Auto : new GridLength(width, GridUnitType.Star) });
        }

        return grid;
    }

    public static Grid Cells(double[] widths, params UIElement[] cells)
    {
        var grid = RowGrid(widths);
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = (FrameworkElement)cells[i];
            if (i > 0)
            {
                cell.Margin = new Thickness(cell.Margin.Left + 12, cell.Margin.Top, cell.Margin.Right, cell.Margin.Bottom);
            }

            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }
}
