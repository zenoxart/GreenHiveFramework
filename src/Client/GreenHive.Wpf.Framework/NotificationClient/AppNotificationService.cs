using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.NotificationClient;

/// <summary>
/// Desktop toasts for notifications that arrive while the app is not in the foreground: a small topmost window in the
/// bottom-right corner of the work area that disappears after a few seconds. Clicking it brings the app to the front and
/// navigates to the notification's target. Toasts stack when several arrive at once.
/// </summary>
public sealed class AppNotificationService : AppObject
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(7);
    private readonly List<Window> _open = [];
    private bool _registered;

    /// <summary>Called with (navigation target, parameter) when a toast is clicked.</summary>
    public Action<string, string?>? Navigate { get; set; }

    /// <summary>Returns true while the main window is in the foreground (then no toast is shown).</summary>
    public Func<bool>? IsForeground { get; set; }

    public void Register() => _registered = true;

    public void Unregister()
    {
        _registered = false;
        foreach (var toast in _open.ToList())
        {
            toast.Close();
        }
    }

    public void Show(InAppNotification notification)
    {
        if (!_registered || IsForeground?.Invoke() == true || notification.Severity == NotificationSeverity.Info && notification.NavigationTarget is null)
        {
            return;
        }

        try
        {
            AppContext.Dispatcher?.Run(() => ShowToast(notification));
        }
        catch (Exception ex)
        {
            AppContext.Log.Write($"Toast could not be shown: {ex.Message}", LogEntryType.Warning, nameof(AppNotificationService));
        }
    }

    private void ShowToast(InAppNotification notification)
    {
        var accent = notification.Severity switch
        {
            NotificationSeverity.Success => Theme.Success,
            NotificationSeverity.Warning => Theme.Warning,
            NotificationSeverity.Error => Theme.Error,
            _ => Theme.BrandBrush,
        };

        var text = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
        text.Children.Add(new TextBlock { Text = "Green Hive", FontSize = 11, Opacity = 0.7 });
        text.Children.Add(new TextBlock { Text = notification.Title, FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        if (!string.IsNullOrWhiteSpace(notification.Message))
        {
            text.Children.Add(new TextBlock { Text = notification.Message, TextWrapping = TextWrapping.Wrap, Opacity = 0.85, Margin = new Thickness(0, 4, 0, 0), MaxHeight = 60 });
        }

        var layout = new DockPanel();
        var bar = new Border { Width = 4, Background = accent };
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(text);

        var card = new Border
        {
            Child = layout,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)),
            Background = Theme.Resolve(Theme.SolidBackground),
            Margin = new Thickness(8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.25 },
            Cursor = notification.NavigationTarget is null ? Cursors.Arrow : Cursors.Hand,
        };

        var toast = new Window
        {
            Content = card,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
        };

        card.MouseLeftButtonUp += (_, _) =>
        {
            toast.Close();
            if (Application.Current?.MainWindow is { } main)
            {
                if (main.WindowState == WindowState.Minimized)
                {
                    main.WindowState = WindowState.Normal;
                }

                main.Activate();
            }

            if (notification.NavigationTarget is { } target)
            {
                Navigate?.Invoke(target, notification.NavigationParameter);
            }
        };

        toast.Loaded += (_, _) => Arrange();
        toast.Closed += (_, _) =>
        {
            _open.Remove(toast);
            Arrange();
        };
        _open.Add(toast);
        toast.Show();

        var timer = new DispatcherTimer { Interval = Lifetime };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!toast.IsMouseOver)
            {
                toast.Close();
            }
            else
            {
                toast.MouseLeave += (_, _) => toast.Close();
            }
        };
        timer.Start();
    }

    private void Arrange()
    {
        var work = SystemParameters.WorkArea;
        var bottom = work.Bottom - 8;
        foreach (var toast in _open.AsEnumerable().Reverse())
        {
            var height = toast.ActualHeight > 0 ? toast.ActualHeight : 110;
            toast.Left = work.Right - toast.Width - 8;
            toast.Top = bottom - height;
            bottom -= height;
        }
    }
}
