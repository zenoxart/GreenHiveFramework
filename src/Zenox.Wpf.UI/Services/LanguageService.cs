using Zenox.Wpf.Views.Helpers;
using Zenox.Wpf.UI.Common.Localization;

namespace Zenox.Wpf.UI.Services
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
