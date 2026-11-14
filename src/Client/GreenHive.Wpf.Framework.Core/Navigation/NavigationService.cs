using GreenHive.Wpf.Framework.Docking;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>
/// Opens navigation targets through the dock layout: documents as tabs, tool windows at their dock side.
/// An already open view is activated instead of opened twice. Records the module history for the wheel.
/// </summary>
public sealed class NavigationService : AppObject
{
    private NavigationRegistry? _registry;
    private ModuleActivationHistory? _history;

    public NavigationRegistry Registry => _registry ??= AppContext.Produce<NavigationRegistry>();

    public ModuleActivationHistory History => _history ??= AppContext.Produce<ModuleActivationHistory>();

    private IDockService Dock => Context<IDockContext>().Dock;

    /// <summary>Opens (or activates) the target.</summary>
    public DockItem? NavigateTo(string key, string? parameter = null)
    {
        var target = Registry.Find(key);
        if (target is null)
        {
            AppContext.Log.Write($"Unknown navigation target '{key}'", LogEntryType.Warning, nameof(NavigationService));
            return null;
        }

        var itemKey = ItemKey(target, parameter);
        if (Dock.Find(itemKey) is { } open)
        {
            Dock.Activate(open);
            return open;
        }

        var item = CreateItem(key, parameter);
        if (item is null)
        {
            return null;
        }

        Dock.Open(item);
        item.ViewModel.OnNavigatedTo(parameter);
        return item;
    }

    /// <summary>Creates a dock item for a target (also used to restore saved layouts).</summary>
    public DockItem? CreateItem(string key, string? parameter)
    {
        var target = Registry.Find(key);
        if (target is null)
        {
            return null;
        }

        var viewModel = target.CreateViewModel(AppContext);
        var titleKey = target.TextKey is null ? null : target.TextKey + ".label";
        if (string.IsNullOrEmpty(viewModel.Title))
        {
            viewModel.Title = titleKey is null ? key : AppContext.Languages.GetText(titleKey);
        }

        return new DockItem
        {
            Key = ItemKey(target, parameter),
            NavigationKey = key,
            ModuleId = target.ModuleId,
            Parameter = parameter,
            Descriptor = target.View,
            ViewModel = viewModel,
            Icon = target.Icon,
            TitleKey = titleKey,
            CreateView = () => target.CreateView(viewModel),
        };
    }

    protected override void OnCreated()
    {
        Dock.ItemResolver = CreateItem;
        Dock.ActiveItemChanged += (_, _) =>
        {
            if (Dock.ActiveItem is { } item)
            {
                History.Record(item.ModuleId);
            }
        };
    }

    private static string ItemKey(NavigationTarget target, string? parameter) =>
        target.View.AllowMultipleInstances && parameter is not null ? $"{target.Key}|{parameter}" : target.Key;
}
