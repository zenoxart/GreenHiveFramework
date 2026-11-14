using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.Wpf.Framework.Security;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.UI.Platform.Shell;

/// <summary>
/// The signed-in user in the title area: picture (or initials) and name. A click opens the profile card with roles,
/// approval limit, last sign-in and connection, and lets the user change or remove the picture, change the password,
/// sign out or sign in.
/// </summary>
internal sealed class ProfileButton : Button
{
    private const int AvatarPixels = 256;
    private readonly ClientAppContext _context;
    private readonly Func<Task> _signIn;
    private readonly Border _avatar = new() { Width = 28, Height = 28, CornerRadius = new CornerRadius(14) };
    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Popup _popup = new() { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };

    public ProfileButton(ClientAppContext context, Func<Task> signIn)
    {
        _context = context;
        _signIn = signIn;
        Padding = new Thickness(6, 2, 8, 2);
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Content = UiKit.Stack(Orientation.Horizontal, 0, _avatar, _name);
        _popup.PlacementTarget = this;
        Click += (_, _) =>
        {
            _popup.Child = BuildCard();
            _popup.IsOpen = true;
        };

        context.Profile.Changed += (_, _) => context.Dispatcher?.Run(Update);
        context.Backend.SignedIn += (_, _) => context.Dispatcher?.Run(Update);
        context.Backend.SignedOut += (_, _) => context.Dispatcher?.Run(Update);
        context.Languages.LanguageChanged += (_, _) => Update();
        Update();
    }

    private string T(string key) => _context.Languages.GetText("shell:" + key);

    private string Z(string key) => _context.Languages.GetText("greenhive:" + key);

    private void Update()
    {
        var user = _context.Backend.CurrentUser;
        _name.Text = user?.DisplayName ?? Z("profile.notSignedIn");
        FillAvatar(_avatar, 12);
        ToolTip = user is null ? T("profile.signin") : $"{user.DisplayName} ({user.UserName})";
    }

    private void FillAvatar(Border target, double fontSize)
    {
        if (_context.Profile.Avatar is { Length: > 0 } bytes && Decode(bytes, AvatarPixels) is { } image)
        {
            target.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            target.Child = null;
            return;
        }

        target.Background = Theme.BrandBrush;
        target.Child = new TextBlock
        {
            Text = _context.Backend.IsSignedIn ? _context.Profile.Initials : "?",
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private FrameworkElement BuildCard()
    {
        var backend = _context.Backend;
        var panel = new SpacedPanel { Spacing = 10, Padding = new Thickness(18), Width = 340 };
        var big = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(32), HorizontalAlignment = HorizontalAlignment.Left };
        FillAvatar(big, 24);

        if (backend.CurrentUser is not { } user)
        {
            panel.Children.Add(big);
            panel.Children.Add(UiKit.Title(Z("profile.notSignedIn")));
            panel.Children.Add(UiKit.Caption(T("profile.server") + ": " + backend.ServerUrl));
            panel.Children.Add(UiKit.Button(T("profile.signin"), async (_, _) =>
            {
                _popup.IsOpen = false;
                await _signIn();
            }, accent: true, glyph: "\uE77B"));
            return Wrap(panel);
        }

        panel.Children.Add(UiKit.Row(big, UiKit.Column(UiKit.Title(user.DisplayName), UiKit.Caption(user.Email), UiKit.Caption(user.UserName))));
        panel.Children.Add(Line(T("profile.roles"), string.Join(", ", user.Roles)));
        panel.Children.Add(Line(T("profile.permissions"), user.Permissions.Contains("*") ? T("profile.allPermissions") : user.Permissions.Count.ToString(CultureInfo.CurrentCulture)));
        if (user.ApprovalLimit > 0)
        {
            panel.Children.Add(Line(T("profile.limit"), UiKit.Money(user.ApprovalLimit, "EUR")));
        }

        if (user.LastLoginAt is { } last)
        {
            panel.Children.Add(Line(T("profile.lastLogin"), last.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
        }

        panel.Children.Add(Line(T("profile.connection"), $"{backend.State} · {backend.ServerUrl}"));
        var pictureButtons = UiKit.Row(
            UiKit.Button(T("profile.changePicture"), async (_, _) => await ChangePictureAsync(), glyph: "\uE722"),
            UiKit.Button(T("profile.removePicture"), async (_, _) => await RunAsync(_context.Profile.RemoveAvatarAsync), glyph: "\uE74D"));
        ((UIElement)pictureButtons.Children[1]).IsEnabled = _context.Profile.Avatar is not null;
        panel.Children.Add(pictureButtons);
        panel.Children.Add(UiKit.Row(
            UiKit.Button(T("profile.changePassword"), async (_, _) => await ChangePasswordAsync(), glyph: "\uE8D7"),
            UiKit.Button(Z("profile.signout"), (_, _) =>
            {
                _popup.IsOpen = false;
                backend.SignOut();
            }, glyph: "\uF3B1")));
        return Wrap(panel);
    }

    private static FrameworkElement Line(string label, string value) =>
        DataList<object>.Cells(new double[] { 2, 3 }, UiKit.Caption(label), new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });

    private static Border Wrap(FrameworkElement content)
    {
        var border = new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(6),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.25 },
        };
        return border.Brush(Border.BackgroundProperty, Theme.SolidBackground).Brush(Border.BorderBrushProperty, Theme.CardStroke);
    }

    private async Task ChangePictureAsync()
    {
        _popup.IsOpen = false;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            var source = Decode(await File.ReadAllBytesAsync(dialog.FileName), AvatarPixels);
            if (source is null)
            {
                throw new InvalidDataException();
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            await RunAsync(() => _context.Profile.SetAvatarAsync(stream.ToArray(), "image/png"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
        {
            ShowError(T("profile.pictureUnreadable"));
        }
    }

    private async Task ChangePasswordAsync()
    {
        _popup.IsOpen = false;
        var current = new PasswordBox();
        var next = new PasswordBox();
        var repeat = new PasswordBox();
        var error = new MessageBar(MessageSeverity.Error) { IsClosable = false };
        var form = UiKit.Column(
            UiKit.Field(T("profile.currentPassword"), current),
            UiKit.Field(T("profile.newPassword"), next),
            UiKit.Field(T("profile.repeatPassword"), repeat),
            UiKit.Caption(T("profile.passwordHint")),
            error);
        form.MinWidth = 340;
        var changed = await UiKit.DialogAsync(this, T("profile.changePassword"), form, T("dialog.save"), T("dialog.cancel"), async () =>
        {
            if (next.Password != repeat.Password)
            {
                error.Show(T("profile.passwordMismatch"));
                return false;
            }

            var result = await _context.Profile.ChangePasswordAsync(current.Password, next.Password);
            if (result.IsFailure)
            {
                error.Show(ErrorMessages.Format(_context.Languages, result.Error));
                return false;
            }

            return true;
        });
        if (changed)
        {
            _context.InApp.Show(T("profile.passwordChanged"));
        }
    }

    private async Task RunAsync(Func<Task<GreenHive.NET.Monads.Result<GreenHive.NET.Monads.Unit>>> action)
    {
        var result = await action();
        if (result.IsFailure)
        {
            ShowError(ErrorMessages.Format(_context.Languages, result.Error));
        }
    }

    private void ShowError(string message) =>
        _context.InApp.Show(T("profile.title"), message, GreenHive.NET.NotificationContracts.NotificationSeverity.Error);

    private static BitmapSource? Decode(byte[] bytes, int size)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = size;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FormatException or IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
