using System.Windows;
using System.Windows.Controls;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.UI.Platform.Shell;

/// <summary>
/// Sign-in dialog (built in code): server URL, user name, password, "stay signed in". "Work offline" continues with the
/// locally installed (development) modules.
/// </summary>
internal static class LoginDialog
{
    /// <summary>Shows the dialog until the sign-in succeeds or the user chooses to work offline. Returns true when signed in.</summary>
    public static Task<bool> ShowAsync(Window owner, ClientAppContext context)
    {
        var languages = context.Languages;
        string T(string key) => languages.GetText("shell:" + key);
        var backend = context.Backend;

        var server = new TextBox { Text = backend.Settings.ServerUrl };
        var user = new TextBox { Text = backend.Settings.LastUserName ?? string.Empty };
        var password = new PasswordBox();
        var remember = new CheckBox { Content = T("login.remember"), IsChecked = backend.Settings.RememberMe };
        var error = new MessageBar(MessageSeverity.Error) { IsClosable = false };
        var progress = new ProgressBar { IsIndeterminate = true, Height = 4, Visibility = Visibility.Collapsed };
        var hint = UiKit.Caption(T("login.hint"));

        var signIn = UiKit.Button(T("login.signin"), (_, _) => { }, accent: true);
        signIn.IsDefault = true;
        signIn.MinWidth = 110;
        var offline = new Button { Content = T("login.offline"), IsCancel = true, MinWidth = 110, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(signIn);
        buttons.Children.Add(offline);

        var logo = new TextBlock { Text = "Green Hive", FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = Theme.BrandBrush };
        var panel = new SpacedPanel { Spacing = 12, Padding = new Thickness(28), MinWidth = 420 };
        panel.Children.Add(logo);
        panel.Children.Add(UiKit.Title(T("login.title")));
        panel.Children.Add(UiKit.Field(T("login.server"), server));
        panel.Children.Add(UiKit.Field(T("login.user"), user));
        panel.Children.Add(UiKit.Field(T("login.password"), password));
        panel.Children.Add(remember);
        panel.Children.Add(progress);
        panel.Children.Add(error);
        panel.Children.Add(hint);
        panel.Children.Add(buttons);

        var dialog = new Window
        {
            Title = T("login.title"),
            Icon = AppIcon.Source,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner.IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
        };
        if (owner.IsVisible)
        {
            dialog.Owner = owner;
        }

        dialog.Brush(Control.BackgroundProperty, Theme.SolidBackground);
        dialog.Loaded += (_, _) => (string.IsNullOrEmpty(user.Text) ? (Control)user : password).Focus();

        var signedIn = false;
        signIn.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(server.Text) || string.IsNullOrWhiteSpace(user.Text) || string.IsNullOrEmpty(password.Password))
            {
                error.Show(T("login.required"));
                return;
            }

            signIn.IsEnabled = false;
            progress.Visibility = Visibility.Visible;
            error.Show(null);
            try
            {
                var result = await backend.SignInAsync(server.Text.Trim(), user.Text.Trim(), password.Password, remember.IsChecked == true);
                if (result.TryGetValue(out _, out var failure))
                {
                    signedIn = true;
                    dialog.DialogResult = true;
                    return;
                }

                error.Show(ErrorMessages.Format(languages, failure));
                password.Password = string.Empty;
                password.Focus();
            }
            finally
            {
                progress.Visibility = Visibility.Collapsed;
                signIn.IsEnabled = true;
            }
        };

        dialog.ShowDialog();
        if (!signedIn)
        {
            context.Log.Write("Working offline (sign-in skipped)", LogEntryType.Warning, nameof(LoginDialog));
        }

        return Task.FromResult(signedIn);
    }
}
