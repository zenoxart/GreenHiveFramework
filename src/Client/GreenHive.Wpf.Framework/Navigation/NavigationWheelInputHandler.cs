using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GreenHive.Wpf.Framework.Commands;

namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>
/// Keyboard handling of the navigation wheel with Alt+Tab semantics: Ctrl+Tab opens it (shown after a short
/// delay), Tab/Shift+Tab and arrows move, releasing Ctrl or Enter confirms, Esc cancels. A quick tap
/// switches straight to the previous module without showing the wheel. The keys are handled in
/// PreviewKeyDown/PreviewKeyUp of the window, so controls such as TabControl cannot swallow them.
/// </summary>
public sealed class NavigationWheelInputHandler
{
    private readonly NavigationWheelViewModel _viewModel;
    private readonly Action<bool> _setVisible;
    private readonly DispatcherTimer _showTimer;
    private bool _keyboardSession;

    public NavigationWheelInputHandler(NavigationWheelViewModel viewModel, UIElement root, Action<bool> setVisible)
    {
        _viewModel = viewModel;
        _setVisible = setVisible;
        _showTimer = new DispatcherTimer(DispatcherPriority.Input, root.Dispatcher) { Interval = TimeSpan.FromMilliseconds(150) };
        _showTimer.Tick += (_, _) =>
        {
            _showTimer.Stop();
            if (_viewModel.IsOpen)
            {
                _setVisible(true);
            }
        };

        root.PreviewKeyDown += OnPreviewKeyDown;
        root.PreviewKeyUp += OnPreviewKeyUp;
    }

    /// <summary>Starts a wheel session (from the Ctrl+Tab shortcut or a menu click).</summary>
    public void Begin(bool reverse)
    {
        if (_viewModel.IsOpen)
        {
            _viewModel.Move(reverse ? -1 : +1);
            return;
        }

        _viewModel.Prepare(reverse);
        _keyboardSession = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (_keyboardSession)
        {
            _showTimer.Start();
        }
        else
        {
            _setVisible(true);
        }
    }

    public void Confirm()
    {
        _showTimer.Stop();
        _setVisible(false);
        _viewModel.Confirm();
    }

    public void Cancel()
    {
        _showTimer.Stop();
        _setVisible(false);
        _viewModel.Cancel();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsOpen)
        {
            return;
        }

        switch (ShortcutInputRouter.RealKey(e))
        {
            case Key.Tab:
                _viewModel.Move(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : +1);
                break;
            case Key.Right:
                _viewModel.Move(+1);
                break;
            case Key.Left:
                _viewModel.Move(-1);
                break;
            case Key.Up:
            case Key.Down:
                _viewModel.SwitchRing();
                break;
            case Key.Enter:
                Confirm();
                break;
            case Key.Escape:
                Cancel();
                break;
            default:
                if (!ShortcutInputRouter.IsModifierKey(ShortcutInputRouter.RealKey(e)))
                {
                    break;
                }

                return;
        }

        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsOpen || !_keyboardSession)
        {
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl && !Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
        {
            e.Handled = true;
            Confirm();
        }
    }
}
