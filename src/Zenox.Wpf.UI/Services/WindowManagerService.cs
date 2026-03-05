using Zenox.Wpf.Views.Helpers;

namespace Zenox.Wpf.UI.Services
{
    /// <summary>
    /// Implementierung des WindowManager Service für UI-Projekt
    /// </summary>
    public class WindowManagerService : IWindowManagerService
    {
        private readonly Zenox.Wpf.UI.Common.WindowManager _windowManager;

        public WindowManagerService()
        {
            _windowManager = Zenox.Wpf.UI.Common.WindowManager.Instance;
        }

        public int GetWindowCount()
        {
            return _windowManager.GetWindowCount();
        }

        public int GetTrackedWindowCount()
        {
            return _windowManager.GetTrackedWindowCount();
        }

        public object GetAllWindowSettings()
        {
            return _windowManager.GetAllWindowSettings();
        }

        public void ClearAllSettings()
        {
            _windowManager.ClearAllSettings();
        }

        public void UpdateGlobalSettings(bool restoreOnStartup, int maxWindowCount, bool autoSave, bool supportMultipleInstances)
        {
            _windowManager.UpdateGlobalSettings(restoreOnStartup, maxWindowCount, autoSave, supportMultipleInstances);
        }

        public object GetGlobalSettings()
        {
            return _windowManager.GetGlobalSettings();
        }
    }
}

