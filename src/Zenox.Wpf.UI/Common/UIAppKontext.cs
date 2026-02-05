using System.ComponentModel;
using Zenox.Wpf.Advanced.MVVM.FactoryInjection;
using Zenox.Wpf.UI.MVVM.ViewModel.Manager;

namespace Zenox.Wpf.UI.Common
{
    public class UIAppKontext : WPFAppKontext, INotifyPropertyChanged
    {
        public UIAppKontext AppContext => this;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region TabViewManager 
        private TabViewManager _tabView;
        public TabViewManager TabView
        {
            get
            {
                if (_tabView == null)
                {
                    _tabView = this.Produziere<TabViewManager>();
                    //this.LogManager.Eintragen(
                    //    $"{_tabView} wurde produziert",
                    //    Data.LogEntryType.NewInstance);
                }
                return _tabView;
            }
        }
        #endregion

        #region MenuManager
        private MenuManager _MenuManager;
        public MenuManager MenuManager
        {
            get
            {
                if (_MenuManager == null)
                {
                    _MenuManager = this.Produziere<MenuManager>();
                }
                return _MenuManager;
            }
        }
        #endregion

        #region AdminView
        private AdminViewManager _AdminView;
        public AdminViewManager AdminView
        {
            get
            {
                if (_AdminView == null)
                {
                    _AdminView = this.Produziere<AdminViewManager>();
                }
                return _AdminView;
            }
            set { _AdminView = value; }
        }
        #endregion
    }
}
