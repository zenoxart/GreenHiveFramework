using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zenox.NET.Core.Common.MVVM.FactoryInjection;
using Zenox.Wpf.Advanced.MVVM.FactoryInjection;
using Zenox.Wpf.UI.Common;
using Zenox.Wpf.UI.MVVM.ViewModel.Manager;

namespace Zenox.Wpf.UI.MVVM.ViewModel
{
    /// <summary>
    /// Definiert das Haupt-ViewModel der Anwendung
    /// </summary>
    /// <remarks>
    /// Integriert ein Factory-Pattern für Dependency-Injection
    /// </remarks>
    public class AppViewModel : WPFAppObject, INotifyPropertyChanged
    {
        public AppViewModel()
        {
            // Nur einen neuen UIAppKontext erstellen, wenn noch keiner vorhanden ist
            if (base.AppKontext == null)
            {
                base.AppKontext = new UIAppKontext();
            }
        }

        /// <summary>
        /// Override AppKontext to use UIAppKontext instead of WPFAppKontext
        /// </summary>
        public new UIAppKontext AppKontext
        {
            get => (UIAppKontext)base.AppKontext;
            set => base.AppKontext = value;
        }

        /// <summary>
        /// INotifyPropertyChanged implementation
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged if the value changed
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Started die Haupt-Ansicht
        /// </summary>
        /// <remarks>
        /// Hängt den Kontext and den DataContext
        /// </remarks>
        public void UIAnzeigen()
        {
            this.StartMelden();
            try
            {
                // Ein View-Objekt des gewünschten Typs

                if (System.Activator
                    .CreateInstance(
                        System.Type.GetType(
                            typeName: Properties.Settings.Default.HauptUITyp)!)
                    is not
                        System.Windows.Window NeuesFenster)
                {
                    throw new Exception("Beim Konfigurationswert HauptUITYp " +
                        "muss es sich um ein Window handeln!");
                }

                // Dieses mit dem ViewModel verknüpfen
                NeuesFenster.DataContext = this;

                // TODO:Initialisieren und Binden (Ersatz für Load Ereignis)

                // Die neue View anzeigen
                NeuesFenster.Show();

            }
            catch (Exception ex)
            {
                this.OnFehlerAufgetreten(
                    new FehlerAufgetretenEventArgs(ex));
            }
            this.EndeMelden();
        }

        #region Manager Properties

        private MainWindowManager? _mainWindow;

        /// <summary>
        /// Ruft den MainWindow-Manager ab
        /// </summary>
        public MainWindowManager MainWindow
        {
            get
            {
                if (_mainWindow == null)
                {
                    if (this.AppKontext == null)
                    {
                        throw new InvalidOperationException("AppKontext ist nicht initialisiert!");
                    }
                    _mainWindow = this.AppKontext.Produziere<MainWindowManager>();
                }
                return _mainWindow;
            }
        }

        private MenuManager? _menuManager;

        /// <summary>
        /// Ruft den Menu-Manager ab
        /// </summary>
        public MenuManager MenuManager
        {
            get
            {
                if (_menuManager == null)
                {
                    if (this.AppKontext == null)
                    {
                        throw new InvalidOperationException("AppKontext ist nicht initialisiert!");
                    }
                    _menuManager = this.AppKontext.Produziere<MenuManager>();
                }
                return _menuManager;
            }
        }

        /// <summary>
        /// Ruft den TabView-Manager ab
        /// </summary>
        public TabViewManager TabView
        {
            get
            {
                if (this.AppKontext is UIAppKontext uiContext)
                {
                    return uiContext.TabView;
                }
                throw new InvalidOperationException("UIAppKontext ist nicht verfügbar!");
            }
        }

        #endregion
    }
}
