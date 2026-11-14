using Microsoft.Maui.Controls;

namespace GreenHive.Maui.UI.Views
{
    public partial class AnzeigeEinstellungenView : ContentPage
    {
        public AnzeigeEinstellungenView()
        {
            InitializeComponent();
            BindingContext = new AnzeigeEinstellungenViewModel();
        }
    }
}