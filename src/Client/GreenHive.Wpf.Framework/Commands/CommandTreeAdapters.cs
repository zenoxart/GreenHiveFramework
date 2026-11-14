using System.Windows.Controls;
using GreenHive.NET.CommandContracts;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>
/// Projects command trees onto WPF menus and tree views. Items get icon, label, tooltip and shortcut from their command
/// and bind to the stable PooledCommand, so menus keep working across a module hot-swap.
/// </summary>
public static class CommandTreeAdapters
{
    /// <summary>Fills a menu bar from a tree: root groups become top-level menu items.</summary>
    public static void FillMenu(Menu menu, RelayCommandPool pool, IReadOnlyList<CommandTreeNode> nodes)
    {
        menu.Items.Clear();
        foreach (var group in nodes.Where(n => n.Kind == CommandTreeNodeKind.Group))
        {
            var item = new MenuItem { Header = AccessText(Label(pool, group)) };
            foreach (var child in CreateMenuItems(pool, group.Children))
            {
                item.Items.Add(child);
            }

            menu.Items.Add(item);
        }
    }

    /// <summary>Creates menu items (menu bar, context menus) for a list of nodes.</summary>
    public static IEnumerable<Control> CreateMenuItems(RelayCommandPool pool, IReadOnlyList<CommandTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node.Kind)
            {
                case CommandTreeNodeKind.Separator:
                    yield return new Separator();
                    break;
                case CommandTreeNodeKind.Group:
                    var sub = new MenuItem { Header = Label(pool, node) };
                    if (node.Icon is not null)
                    {
                        Ui.SetIcon(sub, node.Icon);
                    }

                    foreach (var child in CreateMenuItems(pool, node.Children))
                    {
                        sub.Items.Add(child);
                    }

                    yield return sub;
                    break;
                default:
                    var item = new MenuItem();
                    if (node.CommandKind == CommandKind.Toggle)
                    {
                        item.IsCheckable = false;
                        item.IsChecked = pool.IsChecked(node.CommandId!, null) == true;
                    }

                    Ui.SetCommand(item, node.CommandId);
                    yield return item;
                    break;
            }
        }
    }

    /// <summary>Fills a tree view (e.g. the Module Explorer); leaves execute their command on double click or Enter.</summary>
    public static void FillTreeView(TreeView tree, RelayCommandPool pool, IReadOnlyList<CommandTreeNode> nodes)
    {
        var expanded = new HashSet<string>(tree.Items.OfType<TreeViewItem>().Where(i => i.IsExpanded).Select(i => (string)i.Tag), StringComparer.OrdinalIgnoreCase);
        tree.Items.Clear();
        foreach (var node in nodes)
        {
            if (CreateTreeItem(pool, node, expanded) is { } item)
            {
                tree.Items.Add(item);
            }
        }
    }

    /// <summary>Localized label of a tree node.</summary>
    public static string Label(RelayCommandPool pool, CommandTreeNode node)
    {
        if (node.CommandId is not null)
        {
            return pool.GetLabel(node.CommandId);
        }

        return node.TextKey is null ? node.Id : pool.GetText(node.TextKey, "label") ?? pool.AppContext.Languages.GetText(node.TextKey + ".label");
    }

    private static TreeViewItem? CreateTreeItem(RelayCommandPool pool, CommandTreeNode node, HashSet<string> expanded)
    {
        if (node.Kind == CommandTreeNodeKind.Separator)
        {
            return null;
        }

        var header = new Button { BorderThickness = new System.Windows.Thickness(0), Background = System.Windows.Media.Brushes.Transparent, Padding = new System.Windows.Thickness(2), HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left, Focusable = false };
        var item = new TreeViewItem { Header = header, Tag = node.Id, IsExpanded = node.Kind != CommandTreeNodeKind.Group || expanded.Count == 0 || expanded.Contains(node.Id) };
        if (node.Kind == CommandTreeNodeKind.Group)
        {
            header.IsHitTestVisible = false;
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            if (node.Icon is not null && (pool.AppContext as GreenHive.Wpf.Framework.XamlMetadata.IIconContext)?.Icons is { } icons)
            {
                var icon = GreenHive.Wpf.Framework.Theming.IconFactory.CreateIcon(icons, node.Icon, 14);
                icon.Margin = new System.Windows.Thickness(0, 0, 8, 0);
                panel.Children.Add(icon);
            }

            panel.Children.Add(new TextBlock { Text = Label(pool, node), FontWeight = System.Windows.FontWeights.SemiBold });
            header.Content = panel;
            foreach (var child in node.Children)
            {
                if (CreateTreeItem(pool, child, expanded) is { } childItem)
                {
                    item.Items.Add(childItem);
                }
            }
        }
        else
        {
            Ui.SetCommand(header, node.CommandId);
            header.Focusable = false;
            item.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter && node.CommandId is not null)
                {
                    _ = pool.ExecuteAsync(node.CommandId);
                    e.Handled = true;
                }
            };
        }

        return item;
    }

    private static string AccessText(string label) => label.Replace("_", "__", StringComparison.Ordinal);
}
