using GreenHive.Wpf.Views.Helpers;
using GreenHive.Wpf.UI.Common.Localization;

namespace GreenHive.Wpf.UI.Services
{
    /// <summary>
    /// Implementierung des Language Service
    /// </summary>
    public class LanguageService : ILanguageService
    {
        private readonly LocalizationManager _localizationManager;

        public LanguageService()
        {
            _localizationManager = LocalizationManager.Instance;
        }

        public string GetCurrentLanguage()
        {
            return _localizationManager.GetCurrentLanguage();
        }

        public void ChangeLanguage(string languageCode)
        {
            _localizationManager.ChangeLanguage(languageCode);
        }

        public object GetAvailableLanguages()
        {
            return _localizationManager.GetAvailableLanguages();
        }
    }
}
