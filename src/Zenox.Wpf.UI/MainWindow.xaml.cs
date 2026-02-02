using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Zenox.Wpf.Controls.Common.TabView;
using Zenox.Wpf.UI.MVVM.ViewModel;
using Zenox.Wpf.UI.MVVM.ViewModel.Manager;

namespace Zenox.Wpf.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : UserControl
{
    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += OnMainWindowLoaded;

        // Wire up the TabItemRemoved event (aktivieren falls TabViewControl verwendet wird)
        // tabViewControl.TabItemRemoved += OnTabItemRemoved;

        // Keyboard-Shortcuts für Tutorial
        this.KeyDown += OnKeyDown;
        this.Focusable = true;
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Tutorial-System mit dem Overlay verknüpfen
        if (DataContext is AppViewModel appViewModel)
        {
            appViewModel.MainWindow.InitializeTutorialSystem(
                Window.GetWindow(this),
                TutorialOverlay);
        }

        // Focus setzen für Keyboard-Events
        this.Focus();
    }

    private void OnTabItemRemoved(object sender, TabItemRemovedEventArgs e)
    {
        // Get the DataContext and call the TabViewManager's OnTabItemRemoved method
        if (DataContext is AppViewModel appViewModel)
        {
            appViewModel.TabView.OnTabItemRemoved(e.RemovedTabItem);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is AppViewModel appViewModel)
        {
            switch (e.Key)
            {
                case Key.F1:
                    MainWindowManager.StartTutorialCommand.Execute("MainWindow");
                    e.Handled = true;
                    break;
                case Key.F2:
                    MainWindowManager.StartTutorialCommand.Execute("MenuNavigation");
                    e.Handled = true;
                    break;
            }
        }
    }
}