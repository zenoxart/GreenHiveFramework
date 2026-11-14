using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.UI.Platform.Shell;

/// <summary>
/// The command palette (Ctrl+Shift+P): a search box over every command the user may execute, with the breadcrumb label
/// and the shortcut of each result. Enter executes the selected command, Esc closes.
/// </summary>
internal sealed class CommandPalette
{
    private readonly RelayCommandPool _pool;
    private readonly Popup _popup;
    private readonly TextBox _query = new() { FontSize = 15, Padding = new Thickness(8, 6, 8, 6) };
    private readonly ListBox _results = new() { MaxHeight = 380, BorderThickness = new Thickness(0) };

    public CommandPalette(RelayCommandPool pool, UIElement anchor)
    {
        _pool = pool;
        var panel = new DockPanel { Width = 560 };
        DockPanel.SetDock(_query, Dock.Top);
        panel.Children.Add(_query);
        panel.Children.Add(_results);
        var border = new Border
        {
            Child = panel,
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Theme.BrandBrush,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.3 },
        };
        border.Brush(Border.BackgroundProperty, Theme.SolidBackground);

        _popup = new Popup
        {
            Child = border,
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
        };
        _popup.Opened += (_, _) =>
        {
            _query.Focus();
            Keyboard.Focus(_query);
        };

        _query.TextChanged += (_, _) => Search();
        _query.PreviewKeyDown += OnKey;
        _results.MouseDoubleClick += (_, _) => ExecuteSelected();
    }

    public bool IsOpen => _popup.IsOpen;

    public void Open()
    {
        _query.Text = string.Empty;
        Search();
        _popup.HorizontalOffset = 0;
        _popup.IsOpen = true;
    }

    public void Close() => _popup.IsOpen = false;

    private void Search()
    {
        _results.Items.Clear();
        foreach (var result in _pool.Find(_query.Text, 30))
        {
            var row = new DockPanel { Opacity = result.IsEnabled ? 1 : 0.5 };
            var shortcut = new TextBlock { Text = result.ShortcutText ?? string.Empty, Opacity = 0.7, Margin = new Thickness(16, 0, 0, 0) };
            DockPanel.SetDock(shortcut, Dock.Right);
            row.Children.Add(shortcut);
            row.Children.Add(new TextBlock { Text = result.Label, TextTrimming = TextTrimming.CharacterEllipsis });
            _results.Items.Add(new ListBoxItem { Content = row, Tag = result, Padding = new Thickness(6, 4, 6, 4), IsEnabled = result.IsEnabled });
        }

        _results.SelectedIndex = _results.Items.Cast<ListBoxItem>().ToList().FindIndex(i => i.IsEnabled);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Move(+1);
                e.Handled = true;
                break;
            case Key.Up:
                Move(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void Move(int step)
    {
        if (_results.Items.Count == 0)
        {
            return;
        }

        var index = _results.SelectedIndex;
        for (var i = 0; i < _results.Items.Count; i++)
        {
            index = (index + step + _results.Items.Count) % _results.Items.Count;
            if (((ListBoxItem)_results.Items[index]).IsEnabled)
            {
                _results.SelectedIndex = index;
                _results.ScrollIntoView(_results.Items[index]);
                return;
            }
        }
    }

    private void ExecuteSelected()
    {
        if ((_results.SelectedItem as ListBoxItem)?.Tag is CommandSearchResult { IsEnabled: true } result)
        {
            Close();
            _ = _pool.ExecuteAsync(result.CommandId);
        }
    }
}
