using System.Windows;
using System.Windows.Controls;
using Zenox.Wpf.UI.MVVM.ViewModel;

namespace Zenox.Wpf.UI.Views;

public partial class AuthView : UserControl
{
	public AuthView()
	{
		InitializeComponent();
	}

	private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
	{
		if (DataContext is AuthViewModel viewModel && sender is PasswordBox passwordBox)
		{
			viewModel.Password = passwordBox.Password;
		}
	}
}
