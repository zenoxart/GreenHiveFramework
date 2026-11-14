using GreenHive.NET.CommandContracts;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>
/// Generates hierarchical command trees from the flat list of registrations, for menus and tree views.
/// Missing groups are created implicitly, contributions of several modules are merged, siblings are
/// sorted by section, order and label, separators are inserted between sections and empty groups removed.
/// </summary>
public static class CommandTreeBuilder
{
    public static IReadOnlyList<CommandTreeNode> Build(
        string placement,
        IEnumerable<CommandRegistration> commands,
        IEnumerable<CommandGroupDescriptor> groups,
        Func<CommandRegistration, bool> isVisible,
        Func<string?, string> labelOf)
    {
        var groupInfo = groups
            .Where(g => g.Placement.Equals(placement, StringComparison.OrdinalIgnoreCase))
            .GroupBy(g => g.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var root = new MutableNode(string.Empty, null, null);
        foreach (var registration in commands)
        {
            foreach (var place in registration.Descriptor.Placements)
            {
                if (!place.Placement.Equals(placement, StringComparison.OrdinalIgnoreCase) || !isVisible(registration))
                {
                    continue;
                }

                var parent = GetGroup(root, place.Path, groupInfo);
                parent.Children.Add(new MutableNode(registration.Descriptor.Id, registration, null)
                {
                    Section = place.Section,
                    Order = place.Order,
                    SortLabel = labelOf(registration.Descriptor.TextKey),
                });
            }
        }

        return Convert(root.Children, placement, labelOf);
    }

    private static MutableNode GetGroup(MutableNode root, string path, IReadOnlyDictionary<string, CommandGroupDescriptor> groupInfo)
    {
        var current = root;
        var cumulative = string.Empty;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            cumulative = cumulative.Length == 0 ? segment : $"{cumulative}/{segment}";
            var next = current.Children.FirstOrDefault(c => c.IsGroup && c.Path.Equals(cumulative, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                groupInfo.TryGetValue(cumulative, out var descriptor);
                next = new MutableNode(cumulative, null, descriptor ?? new CommandGroupDescriptor(string.Empty, cumulative))
                {
                    Section = descriptor?.Section ?? 0,
                    Order = descriptor?.Order ?? 0,
                };
                current.Children.Add(next);
            }

            current = next;
        }

        return current;
    }

    private static List<CommandTreeNode> Convert(List<MutableNode> nodes, string placement, Func<string?, string> labelOf)
    {
        foreach (var node in nodes.Where(n => n.IsGroup))
        {
            node.SortLabel = labelOf(GroupTextKey(node));
        }

        var result = new List<CommandTreeNode>();
        int? lastSection = null;
        foreach (var node in nodes.OrderBy(n => n.Section).ThenBy(n => n.Order).ThenBy(n => n.SortLabel, StringComparer.CurrentCultureIgnoreCase))
        {
            CommandTreeNode converted;
            if (node.IsGroup)
            {
                var children = Convert(node.Children, placement, labelOf);
                if (children.Count == 0)
                {
                    continue;
                }

                converted = new CommandTreeNode($"{placement}/{node.Path}", CommandTreeNodeKind.Group)
                {
                    TextKey = GroupTextKey(node),
                    Icon = node.Group?.Icon,
                    Children = children,
                };
            }
            else
            {
                var descriptor = node.Registration!.Descriptor;
                converted = new CommandTreeNode(descriptor.Id, CommandTreeNodeKind.Command)
                {
                    CommandId = descriptor.Id,
                    TextKey = descriptor.TextKey,
                    Icon = descriptor.Icon,
                    CommandKind = descriptor.Kind,
                };
            }

            if (lastSection is not null && lastSection != node.Section)
            {
                result.Add(new CommandTreeNode($"{placement}/separator/{result.Count}", CommandTreeNodeKind.Separator));
            }

            lastSection = node.Section;
            result.Add(converted);
        }

        return result;
    }

    private static string GroupTextKey(MutableNode node)
    {
        if (node.Group?.TextKey is { Length: > 0 } key)
        {
            return key;
        }

        var lastSegment = node.Path.Split('/').Last();
        return $"shell:group.{lastSegment}";
    }

    private sealed class MutableNode(string path, CommandRegistration? registration, CommandGroupDescriptor? group)
    {
        public string Path { get; } = path;

        public CommandRegistration? Registration { get; } = registration;

        public CommandGroupDescriptor? Group { get; } = group;

        public bool IsGroup => Registration is null;

        public int Section { get; init; }

        public int Order { get; init; }

        public string SortLabel { get; set; } = string.Empty;

        public List<MutableNode> Children { get; } = [];
    }
}
