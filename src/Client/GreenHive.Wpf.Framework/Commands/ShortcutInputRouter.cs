using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Mvvm;
using KeyGesture = GreenHive.NET.CommandContracts.KeyGesture;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>
/// Connects the ShortcutManager to the keyboard input of each window: handles PreviewKeyDown on the
/// window (so controls cannot swallow shell shortcuts), resolves single gestures and two-step chords,
/// and never fires plain keys while a text field has focus.
/// </summary>
public sealed class ShortcutInputRouter : AppObject
{
    private static readonly TimeSpan ChordTimeout = TimeSpan.FromSeconds(1.5);
    private KeyGesture? _pendingChord;
    private DateTimeOffset _pendingSince;

    /// <summary>Raised when the first step of a chord was pressed (text to show) or the chord ended (null).</summary>
    public event EventHandler<string?>? ChordPending;

    /// <summary>While true, the router ignores input (e.g. the navigation wheel or shortcut recording is active).</summary>
    public bool IsSuspended { get; set; }

    public void Attach(UIElement root) => root.PreviewKeyDown += OnPreviewKeyDown;

    public void Detach(UIElement root) => root.PreviewKeyDown -= OnPreviewKeyDown;

    /// <summary>The key of a key event (WPF reports keys pressed with Alt as Key.System).</summary>
    public static Key RealKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key,
    };

    /// <summary>The modifier keys currently held down.</summary>
    public static KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        var wpf = Keyboard.Modifiers;
        if (wpf.HasFlag(ModifierKeys.Control)) modifiers |= KeyModifiers.Ctrl;
        if (wpf.HasFlag(ModifierKeys.Shift)) modifiers |= KeyModifiers.Shift;
        if (wpf.HasFlag(ModifierKeys.Alt)) modifiers |= KeyModifiers.Alt;
        if (wpf.HasFlag(ModifierKeys.Windows)) modifiers |= KeyModifiers.Win;
        return modifiers;
    }

    public static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    /// <summary>The gesture of a key event, stored by virtual key (works on every keyboard layout).</summary>
    public static KeyGesture ToGesture(KeyEventArgs e) => new(CurrentModifiers(), KeyInterop.VirtualKeyFromKey(RealKey(e)));

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = RealKey(e);
        if (IsSuspended || IsModifierKey(key))
        {
            return;
        }

        var modifiers = CurrentModifiers();
        var hasCommandModifier = (modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win)) != 0;
        var isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (!hasCommandModifier && !isFunctionKey && IsTextInputFocused())
        {
            return;
        }

        var pool = Context<ICommandContext>().Commands;
        var gesture = new KeyGesture(modifiers, KeyInterop.VirtualKeyFromKey(key));

        if (_pendingChord is { } first)
        {
            _pendingChord = null;
            ChordPending?.Invoke(this, null);
            if (DateTimeOffset.Now - _pendingSince <= ChordTimeout)
            {
                var chordCommand = pool.Shortcuts.Resolve(first with { Then = gesture });
                if (chordCommand is not null)
                {
                    Execute(pool, chordCommand);
                }

                e.Handled = true;
                return;
            }
        }

        if (pool.Shortcuts.IsChordPrefix(gesture))
        {
            _pendingChord = gesture;
            _pendingSince = DateTimeOffset.Now;
            ChordPending?.Invoke(this, pool.Shortcuts.Format(gesture));
            e.Handled = true;
            return;
        }

        var command = pool.Shortcuts.Resolve(gesture);
        if (command is not null && pool.CanExecute(command, null))
        {
            Execute(pool, command);
            e.Handled = true;
        }
    }

    private void Execute(RelayCommandPool pool, string commandId)
    {
        AppContext.Log.Write($"Shortcut -> {commandId}", LogEntryType.Information, nameof(ShortcutInputRouter));
        _ = pool.ExecuteAsync(commandId);
    }

    private static bool IsTextInputFocused() => Keyboard.FocusedElement is TextBox or PasswordBox or RichTextBox;
}
