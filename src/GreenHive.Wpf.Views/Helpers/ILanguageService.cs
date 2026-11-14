namespace GreenHive.Wpf.Views.Helpers
{
    /// <summary>
    /// Interface für Language Service
    /// </summary>
    public interface ILanguageService
    {
        string GetCurrentLanguage();
        void ChangeLanguage(string languageCode);
        object GetAvailableLanguages();
    }

    /// <summary>
    /// Service Locator für Language Service
    /// </summary>
    public static class LanguageServiceLocator
    {
        public static ILanguageService? Instance { get; set; }
    }
}
