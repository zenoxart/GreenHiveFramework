using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreenHive.Wpf.Controls.Common.CommandHistory;
using GreenHive.Wpf.Controls.Common.CommandPalette;

namespace GreenHive.Wpf.UI
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
            
            // Globale Keyboard-Shortcuts registrieren
            this.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Shift+P - Command Palette
            if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                CommandPaletteManager.Instance.ShowPalette();
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+H - Command History
            if (e.Key == Key.H && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                CommandHistoryManager.Instance.ShowHistory();
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+R - Command Replay Demo (optional - nur wenn View geladen werden soll)
            // Dies wird bereits über die Command Palette gehandhabt
        }
    }
}
