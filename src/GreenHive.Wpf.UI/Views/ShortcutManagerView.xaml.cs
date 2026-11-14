using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GreenHive.Wpf.UI.Views;

public partial class ShortcutManagerView : UserControl
{
    public ShortcutManagerView()
    {
        InitializeComponent();
    }

    private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier keys alone
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        
        if (key == Key.Escape)
        {
            // Clear the recording state
            if (sender is TextBox textBox && textBox.DataContext is MVVM.ViewModel.ShortcutItemViewModel vm)
            {
                vm.IsRecording = false;
                textBox.IsReadOnly = true;
            }
            return;
        }

        if (key == Key.Back || key == Key.Delete)
        {
            // Clear the shortcut
            if (sender is TextBox textBox && textBox.DataContext is MVVM.ViewModel.ShortcutItemViewModel vm)
            {
                vm.KeyGesture = null;
                vm.IsRecording = false;
                textBox.IsReadOnly = true;
            }
            return;
        }

        // Create the new key gesture
        try
        {
            var gesture = new KeyGesture(key, modifiers);
            
            if (sender is TextBox textBox && textBox.DataContext is MVVM.ViewModel.ShortcutItemViewModel vm)
            {
                vm.KeyGesture = gesture;
                vm.IsRecording = false;
                textBox.IsReadOnly = true;
            }
        }
        catch
        {
            // Invalid key combination
        }
    }

    private void ShortcutTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is MVVM.ViewModel.ShortcutItemViewModel vm)
        {
            vm.IsRecording = true;
            textBox.IsReadOnly = false;
            textBox.Text = "Taste drücken...";
        }
    }

    private void ShortcutTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is MVVM.ViewModel.ShortcutItemViewModel vm)
        {
            vm.IsRecording = false;
            textBox.IsReadOnly = true;
        }
    }
}
