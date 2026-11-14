using System.Windows;
using System.Windows.Controls;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>Builds the standard tooltip: label + shortcut on the first line, explanation below.</summary>
public static class RichToolTipBuilder
{
    public static object Build(string? label, string? shortcut, string? description)
    {
        var panel = new StackPanel { MaxWidth = 320 };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(label))
        {
            header.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        }

        if (!string.IsNullOrEmpty(shortcut))
        {
            header.Children.Add(new TextBlock { Text = shortcut, Opacity = 0.7, Margin = new Thickness(header.Children.Count > 0 ? 12 : 0, 0, 0, 0) });
        }

        if (header.Children.Count > 0)
        {
            panel.Children.Add(header);
        }

        if (!string.IsNullOrEmpty(description) && description != label)
        {
            panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Opacity = 0.85, Margin = new Thickness(0, 2, 0, 0) });
        }

        return panel;
    }
}
