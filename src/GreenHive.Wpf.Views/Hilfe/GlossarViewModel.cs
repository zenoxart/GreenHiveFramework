using System.Collections.ObjectModel;

namespace GreenHive.Wpf.Views.Hilfe
{
    public class GlossarEintrag
    {
        public string Term { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }

    public class GlossarViewModel
    {
        public ObservableCollection<GlossarEintrag> Eintraege { get; } = new();

        public GlossarViewModel()
        {
            // Minimalbefuellung — kann spaeter aus Datei/Resource geladen werden
            Eintraege.Add(new GlossarEintrag { Term = "API", Definition = "Application Programming Interface - Schnittstelle zwischen Anwendungen." });
            Eintraege.Add(new GlossarEintrag { Term = "TabView", Definition = "Zentrales UI-Element fuer das Verwalten von Ansichten in der Anwendung." });
            Eintraege.Add(new GlossarEintrag { Term = "RelayCommand", Definition = "Ein einfacher ICommand-Wrapper fuer MVVM-Commands." });
        }
    }
}