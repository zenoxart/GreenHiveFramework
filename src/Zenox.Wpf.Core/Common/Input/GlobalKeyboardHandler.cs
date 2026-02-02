using System.Windows;
using System.Windows.Input;

namespace Zenox.Wpf.Core.Common.Input
{
	/// <summary>
	/// Globaler Keyboard Handler für Tastenkombinationen
	/// </summary>
	public static class GlobalKeyboardHandler
	{
		private static bool _isInitialized;
		private static Action? _showCommandPaletteAction;

		/// <summary>
		/// Initialisiert den globalen Keyboard Handler
		/// </summary>
		/// <param name="showCommandPaletteAction">Action zum Öffnen der Command Palette</param>
		public static void Initialize(Action showCommandPaletteAction)
		{
			if (_isInitialized)
				return;

			_showCommandPaletteAction = showCommandPaletteAction;

			// Event Handler für alle Windows registrieren
			EventManager.RegisterClassHandler(
				typeof(Window),
				Window.PreviewKeyDownEvent,
				new KeyEventHandler(OnPreviewKeyDown));

			_isInitialized = true;
		}

		private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
		{
			// Ctrl+K oder Ctrl+Shift+P öffnet die Command Palette
			if ((e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control) ||
				(e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
			{
				_showCommandPaletteAction?.Invoke();
				e.Handled = true;
			}
		}

		/// <summary>
		/// Deinitialisiert den globalen Keyboard Handler
		/// </summary>
		public static void Shutdown()
		{
			_isInitialized = false;
			_showCommandPaletteAction = null;
		}
	}
}
