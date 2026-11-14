using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreenHive.NET.Core.Common.Command;

namespace GreenHive.Wpf.Controls.Common.TabView
{
    /// <summary>
    /// Interaktionslogik für TabViewControl.xaml
    /// </summary>
    public partial class TabViewControl : UserControl
    {
        public TabViewControl()
        {
            InitializeComponent();
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            CloseTabCommand = new RelayCommand(CloseTab, CanCloseTab);
            CloseAllOtherTabsCommand = new RelayCommand(CloseAllOtherTabs, CanCloseAllOtherTabs);
            CloseAllTabsExceptCurrentCommand = new RelayCommand(CloseAllTabsExceptCurrent, CanCloseAllTabsExceptCurrent);
            TogglePinCommand = new RelayCommand(TogglePin, CanTogglePin);
            CloseAllUnpinnedTabsCommand = new RelayCommand(CloseAllUnpinnedTabs, CanCloseAllUnpinnedTabs);
            CloseAllTabsToRightCommand = new RelayCommand(CloseAllTabsToRight, CanCloseAllTabsToRight);
        }

        #region Commands

        public ICommand CloseTabCommand
        {
            get { return (ICommand)GetValue(CloseTabCommandProperty); }
            set { SetValue(CloseTabCommandProperty, value); }
        }

        public static readonly DependencyProperty CloseTabCommandProperty =
            DependencyProperty.Register("CloseTabCommand", typeof(ICommand), typeof(TabViewControl), new PropertyMetadata(null));

        public ICommand CloseAllOtherTabsCommand
        {
            get { return (ICommand)GetValue(CloseAllOtherTabsCommandProperty); }
            set { SetValue(CloseAllOtherTabsCommandProperty, value); }
        }

        public static readonly DependencyProperty CloseAllOtherTabsCommandProperty =
            DependencyProperty.Register("CloseAllOtherTabsCommand", typeof(ICommand), typeof(TabViewControl), new PropertyMetadata(null));

        public ICommand CloseAllTabsExceptCurrentCommand
        {
            get { return (ICommand)GetValue(CloseAllTabsExceptCurrentCommandProperty); }
            set { SetValue(CloseAllTabsExceptCurrentCommandProperty, value); }
        }

        public static readonly DependencyProperty CloseAllTabsExceptCurrentCommandProperty =
            DependencyProperty.Register("CloseAllTabsExceptCurrentCommand", typeof(ICommand), typeof(TabViewControl), new PropertyMetadata(null));

        public ICommand TogglePinCommand
        {
            get { return (ICommand)GetValue(TogglePinCommandProperty); }
            set { SetValue(TogglePinCommandProperty, value); }
        }

        public static readonly DependencyProperty TogglePinCommandProperty =
            DependencyProperty.Register("TogglePinCommand", typeof(ICommand), typeof(TabViewControl), new PropertyMetadata(null));

        public ICommand CloseAllUnpinnedTabsCommand
        {
            get { return (ICommand)GetValue(CloseAllUnpinnedTabsCommandProperty); }
            set { SetValue(CloseAllUnpinnedTabsCommandProperty, value); }
        }

        public static readonly DependencyProperty CloseAllUnpinnedTabsCommandProperty =
            DependencyProperty.Register("CloseAllUnpinnedTabsCommand", typeof(ICommand), typeof(TabViewControl), new PropertyMetadata(null));

        public ICommand CloseAllTabsToRightCommand
        {
            get { return (ICommand)GetValue(CloseAllTabsToRightCommandProperty); }
            set { SetValue(CloseAllTabsToRightCommandProperty, value); }
        }

        public static readonly DependencyProperty CloseAllTabsToRightCommandProperty =
            DependencyProperty.Register("CloseAllTabsToRightCommand", typeof(ICommand), typeof(TabViewControl), new PropertyMetadata(null));

        #endregion

        #region Command Implementations

        private void CloseTab(object parameter)
        {
            if (parameter is Core.ExtDto.TabItem tabItem && TabItems != null && !tabItem.IsPinned)
            {
                // Notify about tab removal before actually removing it
                TabItemRemoved?.Invoke(this, new TabItemRemovedEventArgs(tabItem));

                TabItems.Remove(tabItem);

                // If this was the selected tab and there are other tabs, select the first one
                if (TabItems.Count > 0 && tabControl.SelectedIndex == -1)
                {
                    tabControl.SelectedIndex = 0;
                }
            }
        }

        private bool CanCloseTab(object parameter)
        {
            return parameter is Core.ExtDto.TabItem tabItem &&
                   TabItems != null &&
                   TabItems.Count > 0 &&
                   !tabItem.IsPinned;
        }

        private void CloseAllOtherTabs(object parameter)
        {
            if (parameter is Core.ExtDto.TabItem currentTab && TabItems != null)
            {
                var tabsToRemove = TabItems.Where(tab => tab != currentTab && !tab.IsPinned).ToList();
                foreach (var tab in tabsToRemove)
                {
                    // Notify about tab removal before actually removing it
                    TabItemRemoved?.Invoke(this, new TabItemRemovedEventArgs(tab));
                    TabItems.Remove(tab);
                }
            }
        }

        private bool CanCloseAllOtherTabs(object parameter)
        {
            return parameter is Core.ExtDto.TabItem currentTab &&
                   TabItems != null &&
                   TabItems.Count(tab => tab != currentTab && !tab.IsPinned) > 0;
        }

        private void CloseAllTabsExceptCurrent(object parameter)
        {
            if (parameter is Core.ExtDto.TabItem currentTab && TabItems != null)
            {
                var tabsToRemove = TabItems.Where(tab => tab != currentTab && !tab.IsPinned).ToList();
                foreach (var tab in tabsToRemove)
                {
                    // Notify about tab removal before actually removing it
                    TabItemRemoved?.Invoke(this, new TabItemRemovedEventArgs(tab));
                    TabItems.Remove(tab);
                }
            }
        }

        private bool CanCloseAllTabsExceptCurrent(object parameter)
        {
            return parameter is Core.ExtDto.TabItem currentTab &&
                   TabItems != null &&
                   TabItems.Count(tab => tab != currentTab && !tab.IsPinned) > 0;
        }

        private void TogglePin(object parameter)
        {
            if (parameter is Core.ExtDto.TabItem tabItem)
            {
                tabItem.IsPinned = !tabItem.IsPinned;
            }
        }

        private bool CanTogglePin(object parameter)
        {
            return parameter is Core.ExtDto.TabItem;
        }

        private void CloseAllUnpinnedTabs(object parameter)
        {
            if (TabItems != null)
            {
                var tabsToRemove = TabItems.Where(tab => !tab.IsPinned).ToList();
                foreach (var tab in tabsToRemove)
                {
                    // Notify about tab removal before actually removing it
                    TabItemRemoved?.Invoke(this, new TabItemRemovedEventArgs(tab));
                    TabItems.Remove(tab);
                }
            }
        }

        private bool CanCloseAllUnpinnedTabs(object parameter)
        {
            return TabItems != null && TabItems.Any(tab => !tab.IsPinned);
        }

        private void CloseAllTabsToRight(object parameter)
        {
            if (parameter is Core.ExtDto.TabItem currentTab && TabItems != null)
            {
                var currentIndex = TabItems.IndexOf(currentTab);
                if (currentIndex >= 0)
                {
                    var tabsToRemove = TabItems.Skip(currentIndex + 1).Where(tab => !tab.IsPinned).ToList();
                    foreach (var tab in tabsToRemove)
                    {
                        // Notify about tab removal before actually removing it
                        TabItemRemoved?.Invoke(this, new TabItemRemovedEventArgs(tab));
                        TabItems.Remove(tab);
                    }
                }
            }
        }

        private bool CanCloseAllTabsToRight(object parameter)
        {
            if (parameter is Core.ExtDto.TabItem currentTab && TabItems != null)
            {
                var currentIndex = TabItems.IndexOf(currentTab);
                return currentIndex >= 0 &&
                       TabItems.Skip(currentIndex + 1).Any(tab => !tab.IsPinned);
            }
            return false;
        }

        #endregion

        #region Events

        public event EventHandler<TabItemRemovedEventArgs> TabItemRemoved;

        #endregion

        #region Properties

        public Core.ExtDto.TabItems TabItems
        {
            get { return (Core.ExtDto.TabItems)GetValue(TabItemsProperty); }
            set { SetValue(TabItemsProperty, value); }
        }

        public static readonly DependencyProperty TabItemsProperty =
            DependencyProperty.Register("TabItems", typeof(Core.ExtDto.TabItems), typeof(TabViewControl), new PropertyMetadata(null, OnTabItemsChanged));

        private static void OnTabItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TabViewControl control)
            {
                // Subscribe to events when TabItems collection changes
                if (e.OldValue is Core.ExtDto.TabItems oldCollection)
                {
                    // Unsubscribe from old collection if needed
                }

                if (e.NewValue is Core.ExtDto.TabItems newCollection)
                {
                    // Subscribe to new collection if needed
                }
            }
        }

        #endregion
    }
}
