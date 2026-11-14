namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>Implemented by application contexts that provide navigation.</summary>
public interface INavigationContext
{
    NavigationService Navigation { get; }
}
