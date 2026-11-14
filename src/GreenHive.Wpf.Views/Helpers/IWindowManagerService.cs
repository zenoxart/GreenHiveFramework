namespace GreenHive.Wpf.Views.Helpers
{
    /// <summary>
    /// Wrapper für WindowManager - wird vom UI-Projekt implementiert
    /// </summary>
    public interface IWindowManagerService
    {
        int GetWindowCount();
        int GetTrackedWindowCount();
        object GetAllWindowSettings();
        void ClearAllSettings();
        void UpdateGlobalSettings(bool restoreOnStartup, int maxWindowCount, bool autoSave, bool supportMultipleInstances);
        object GetGlobalSettings();
    }

    /// <summary>
    /// Statischer Service-Locator für den WindowManager
    /// </summary>
    public static class WindowManagerServiceLocator
    {
        public static IWindowManagerService? Instance { get; set; }
    }
}
