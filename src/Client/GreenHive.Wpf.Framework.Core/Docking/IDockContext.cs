using GreenHive.NET.ModuleContracts;

namespace GreenHive.Wpf.Framework.Docking;

/// <summary>The dock layout as seen by navigation, modules and the hot-swap (implemented by the WPF DockManager).</summary>
public interface IDockService
{
    event EventHandler? ActiveItemChanged;

    /// <summary>Creates a dock item for a navigation key + parameter (used to restore saved layouts).</summary>
    Func<string, string?, DockItem?>? ItemResolver { get; set; }

    DockItem? ActiveItem { get; }

    IReadOnlyCollection<DockItem> OpenItems { get; }

    /// <summary>Open items, most recently activated first.</summary>
    IReadOnlyList<DockItem> ActivationOrder { get; }

    DockItem? Find(string key);

    void Open(DockItem item, DockSide? location = null, bool activate = true);

    void Activate(DockItem item);

    bool Close(DockItem? item);
}

/// <summary>Implemented by application contexts that provide the dock layout.</summary>
public interface IDockContext
{
    IDockService Dock { get; }
}
