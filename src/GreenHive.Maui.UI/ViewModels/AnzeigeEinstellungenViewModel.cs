using GreenHive.NET.Core.Common.Neuigkeiten;
using System.Collections.ObjectModel;

namespace GreenHive.Maui.UI.ViewModels
{
    public class AnzeigeEinstellungenViewModel
    {
        public AnzeigeEinstellungen AnzeigeEinstellungen { get; set; } = new AnzeigeEinstellungen();
        public ObservableCollection<string> SortOrders { get; } = new ObservableCollection<string> { "Datum", "Priorität" };
        public ObservableCollection<string> Themes { get; } = new ObservableCollection<string> { "Light", "Dark" };
    }
}