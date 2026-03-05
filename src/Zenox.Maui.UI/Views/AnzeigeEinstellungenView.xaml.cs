using Microsoft.Maui.Controls;

namespace Zenox.Maui.UI.Views
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