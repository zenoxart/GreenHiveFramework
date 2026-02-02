using System.Windows;
using System.Windows.Controls;

namespace Zenox.Wpf.UI
{
    /// <summary>
    /// Interaktionslogik für MainWindowShell.xaml
    /// </summary>
    public partial class MainAppShell : Window
    {
        public ContentControl MainModule => MainModul;

        public MainAppShell()
        {
            InitializeComponent();
        }
    }
}
