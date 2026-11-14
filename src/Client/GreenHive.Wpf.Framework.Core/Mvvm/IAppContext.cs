namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// Marks an object that works with the application infrastructure (WIFI: IAppKontext).
/// The context is set exactly once by <see cref="ApplicationContext.Produce{T}"/>.
/// </summary>
public interface IAppContext
{
    ApplicationContext AppContext { get; set; }
}
