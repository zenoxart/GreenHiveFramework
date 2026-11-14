using System.Collections.ObjectModel;
using GreenHive.Wpf.UI.Platform;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.UI.Platform.Views;

/// <summary>A module tile on the start page.</summary>
public sealed record ModuleCard(string NavigationKey, string Label, string Description, string Glyph);

/// <summary>ViewModel of the start page.</summary>
public sealed class StartViewModel : ViewModelAppObject
{
    public ObservableCollection<ModuleCard> Modules { get; } = [];

    private ClientAppContext Client => Context<ClientAppContext>();

    public void Open(ModuleCard card) => Client.Navigation.NavigateTo(card.NavigationKey);

    protected override void OnCreated()
    {
        Fill();
        AppContext.Languages.LanguageChanged += (_, _) => Fill();
        Client.Modules.Changed += (_, _) => AppContext.Dispatcher?.Run(Fill);
    }

    private void Fill()
    {
        Modules.Clear();
        foreach (var home in Client.Navigation.Registry.ModuleHomes.Where(h => h.Key != BuiltInViews.Start))
        {
            var baseKey = home.TextKey ?? home.Key;
            if (!AppContext.Languages.TryGetText(baseKey + ".description", out var description))
            {
                AppContext.Languages.TryGetText(baseKey + ".tooltip", out description);
            }

            Modules.Add(new ModuleCard(home.Key, AppContext.Languages.GetText(baseKey + ".label"), description, Client.Icons.ResolveGlyph(home.Icon)));
        }
    }
}
