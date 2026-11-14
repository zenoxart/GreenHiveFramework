namespace GreenHive.Wpf.Framework.XamlMetadata;

/// <summary>Implemented by application contexts that provide the icon catalogue.</summary>
public interface IIconContext
{
    IconCatalog Icons { get; }
}
