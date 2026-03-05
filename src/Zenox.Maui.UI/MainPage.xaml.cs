using Zenox.Maui.UI.MVVM;
using Zenox.Maui.UI.Pages;

namespace Zenox.Maui.UI
{
    /// <summary>
    /// MainPage als TabbedPage mit mehreren Tab-Seiten
    /// </summary>
    public partial class MainPage : TabbedPage
    {
        private readonly MAUIAppKontext _appKontext;

        public MainPage()
        {
            // AppKontext initialisieren
            _appKontext = MAUIAppKontext.Instance;
            
            InitializeComponent();

            // Optional: Event für Tab-Wechsel
            CurrentPageChanged += OnCurrentPageChanged;
        }

        private void OnCurrentPageChanged(object? sender, EventArgs e)
        {
            if (CurrentPage != null)
            {
                // Aktualisiere den Titel im MenuManager
                _appKontext.MenuManager.CurrentPageTitle = CurrentPage.Title ?? "Unbekannt";
                
                System.Diagnostics.Debug.WriteLine($"Tab gewechselt zu: {CurrentPage.Title}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            CurrentPageChanged -= OnCurrentPageChanged;
        }
    }
}
