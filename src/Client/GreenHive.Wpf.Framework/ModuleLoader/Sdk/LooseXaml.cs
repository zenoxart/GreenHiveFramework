using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>
/// Views from loose XAML (manifest viewMode "looseXaml"): the .xaml file is an embedded resource of the module and is
/// parsed at run time with XamlReader, so no compiled BAML/generated classes are involved and the module stays
/// hot-swappable. Only {Binding} works (no x:Class, no event handlers in markup); the ViewModel becomes the DataContext.
/// </summary>
public static class LooseXaml
{
    /// <summary>Loads an embedded XAML resource (name ends with <paramref name="resourceSuffix"/>, e.g. "Views.Items.xaml").</summary>
    public static FrameworkElement Load(Assembly assembly, string resourceSuffix, object? dataContext = null)
    {
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Embedded XAML '{resourceSuffix}' not found in {assembly.GetName().Name}.");
        using var stream = assembly.GetManifestResourceStream(name)!;
        var context = new ParserContext { BaseUri = new Uri($"pack://application:,,,/{assembly.GetName().Name};component/") };
        var element = (FrameworkElement)XamlReader.Load(stream, context);
        element.DataContext = dataContext;
        return element;
    }
}
