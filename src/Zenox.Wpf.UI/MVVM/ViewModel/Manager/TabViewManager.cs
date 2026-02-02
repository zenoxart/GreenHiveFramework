using System.Windows;
using System.Windows.Controls;
using Zenox.Wpf.Core.ExtDto;
using TabItem = Zenox.Wpf.Core.ExtDto.TabItem;

namespace Zenox.Wpf.UI.MVVM.ViewModel.Manager
{
    /// <summary>
    /// Manages the tab views within an application, allowing for dynamic loading and removal of user controls as tab
    /// items.
    /// </summary>
    /// <remarks>The <see cref="TabViewManager"/> class provides functionality to load user controls as tab
    /// items and manage their lifecycle. It maintains a collection of loaded user controls and updates the current view
    /// control as needed. The class also supports event handling for the removal of tab items.</remarks>
    public class TabViewManager : BaseManager
    {
        private TabItems _tabItems;

        public TabItems TabItems
        {
            get
            {
                if (_tabItems == null)
                {
                    _tabItems = new TabItems();
                }
                return _tabItems;
            }
            set
            {
                if (_tabItems != value)
                {
                    _tabItems = value;
                    OnPropertyChanged();
                }
            }
        }

        private TabItem? _selectedTabItem;

        public TabItem? SelectedTabItem
        {
            get => _selectedTabItem;
            set
            {
                if (_selectedTabItem != value)
                {
                    _selectedTabItem = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Lädt eine View in einen neuen Tab
        /// </summary>
        /// <typeparam name="T">Der Typ der View</typeparam>
        /// <param name="title">Der Titel des Tabs (optional)</param>
        public void LoadView<T>(string? title = null) where T : UserControl, new()
        {
            try
            {

                // View direkt als T instanziieren (kein Activator.CreateInstance nötig)
                var view = new T();
                var tabTitle = title ?? typeof(T).Name.Replace("View", "");

                System.Diagnostics.Debug.WriteLine($"Versuche {tabTitle} zu laden...");
                view.DataContext = this.AppKontext;

                // Prüfen, ob bereits ein Tab mit diesem Titel existiert
                var existingTab = TabItems.FirstOrDefault(t => t.Name == tabTitle);
                if (existingTab != null)
                {
                    SelectedTabItem = existingTab;
                    return;
                }

                var tabItem = new TabItem
                {
                    Name = tabTitle,
                    Content = view,
                    IsPinned = false
                };

                TabItems.Add(tabItem);
                SelectedTabItem = tabItem;

                System.Diagnostics.Debug.WriteLine($"{tabTitle} erfolgreich geladen");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der View: {ex.Message}");
                MessageBox.Show($"Fehler beim Laden der View: {ex.Message}");
            }
        }

        /// <summary>
        /// Wird aufgerufen, wenn ein Tab entfernt wurde
        /// </summary>
        /// <param name="removedTabItem">Das entfernte Tab-Item</param>
        public void OnTabItemRemoved(TabItem removedTabItem)
        {
            // Cleanup-Logic für das entfernte Tab
            if (removedTabItem.Content is IDisposable disposableContent)
            {
                disposableContent.Dispose();
            }

            // Falls das entfernte Tab das aktuell ausgewählte war
            if (SelectedTabItem == removedTabItem)
            {
                SelectedTabItem = TabItems.FirstOrDefault();
            }
        }

        /// <summary>
        /// Schließt alle Tabs
        /// </summary>
        public void CloseAllTabs()
        {
            var tabsToClose = TabItems.Where(t => !t.IsPinned).ToList();
            foreach (var tab in tabsToClose)
            {
                OnTabItemRemoved(tab);
                TabItems.Remove(tab);
            }
        }

        /// <summary>
        /// Schließt einen spezifischen Tab
        /// </summary>
        /// <param name="tabItem">Das zu schließende Tab</param>
        public void CloseTab(TabItem tabItem)
        {
            if (tabItem != null && !tabItem.IsPinned && TabItems.Contains(tabItem))
            {
                OnTabItemRemoved(tabItem);
                TabItems.Remove(tabItem);
            }
        }
    }

}
