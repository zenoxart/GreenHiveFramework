using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Zenox.NET.Core.Common.Command;
using Zenox.NET.Core.Common.Connection;
using Zenox.NET.Core.Common.MVVM.FactoryInjection.Localisation;
using Zenox.NET.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.NET.Core.Common.MVVM.FactoryInjection
{
    /// <summary>
    /// Stellt den Kontext der Anwendung dar, der für die Verwaltung von Anwendungsobjekten und deren Abhängigkeiten verantwortlich ist.
    /// </summary>
    public partial class AppKontext : NotificationObject
    {
        // Thread-sichere Dictionary für generische Manager-Instanzen
        private readonly ConcurrentDictionary<Type, AppObject> _managerCache = new();

        /// <summary>
        /// Gibt einen Manager generisch zurück und erzeugt ihn bei Bedarf.
        /// </summary>
        /// <typeparam name="T">Der Typ des Managers</typeparam>
        /// <returns>Die Instanz des Managers</returns>
        public virtual T GetManager<T>() where T : AppObject, new()
        {
            return (T)_managerCache.GetOrAdd(typeof(T), _ =>
            {
                return this.Produziere<T>();
            });
        }

        #region Anwendungsobjekt-Fabrik
        

        /// <summary>
        /// Erzeugt eine neue Instanz des angegebenen Anwendungsobjekts.
        /// </summary>
        /// <typeparam name="T">Der Typ der zu Instanzieren ist</typeparam>
        /// <returns></returns>
        public virtual T Produziere<T>()
            where T : AppObject, new()
        {
            T NeuesObjekt = new()
            {
                AppKontext = this
            };

#if DEBUG
            NeuesObjekt.FehlerAufgetreten
                += (sender, e)
                => System.Diagnostics.Debug.WriteLine(
                    $"==> FEHLER! {sender} Ausnahme \"{e.Ursache.Message}\"");

            System.Diagnostics.Debug.WriteLine(
                $"==> {NeuesObjekt} produziert und initialisiert...");
#endif
            if (_log != null)
            {
                this.Logger.Eintragen($"==> {NeuesObjekt} produziert und initialisiert...");
            }
            NeuesObjekt.FehlerAufgetreten
                += (sender, e)
                => this.Logger?.Eintragen(
                    $"==> FEHLER! {sender} Ausnahme \"{e.Ursache.Message}\"",
                    LogEintragTyp.Fehler);

            // Hier weitere Initalisierungen ergänzen

            return NeuesObjekt;
        }

        /// <summary>
        /// Erzeugt eine neue Instanz des angegebenen Anwendungsobjekts mit Parametern.
        /// </summary>
        /// <typeparam name="T">Der Typ der zu Instanzieren ist</typeparam>
        /// <param name="param">Parameter für den Konstruktor</param>
        /// <returns></returns>
        public virtual T Produziere<T>(object[] param)
            where T : AppObject
        {
            T NeuesObjekt;

            if (param == null || param.Length == 0)
            {
                NeuesObjekt = Activator.CreateInstance<T>();
            }
            else
            {
                var paramTypes = param.Select(p => p?.GetType() ?? typeof(object)).ToArray();
                var ctor = typeof(T).GetConstructor(paramTypes) ?? throw new ArgumentException($"Kein passender Konstruktor für Typ {typeof(T).Name} mit den angegebenen Parametern gefunden.");
                NeuesObjekt = (T)ctor.Invoke(param);
            }

            NeuesObjekt.AppKontext = this;
#if DEBUG
            NeuesObjekt.FehlerAufgetreten
                += (sender, e)
                => System.Diagnostics.Debug.WriteLine(
                    $"==> FEHLER! {sender} Ausnahme \"{e.Ursache.Message}\"");

            System.Diagnostics.Debug.WriteLine(
                $"==> {NeuesObjekt} produziert und initialisiert...");
#endif
            if (this._log != null)
            {
                this.Logger.Eintragen($"==> {NeuesObjekt} produziert und initialisiert...");
            }
            NeuesObjekt.FehlerAufgetreten
                += (sender, e)
                => this.Logger.Eintragen(
                    $"==> FEHLER! {sender} Ausnahme \"{e.Ursache.Message}\"",
                    LogEintragTyp.Fehler);

            // Hier weitere Initalisierungen ergänzen

            return NeuesObjekt;
        }

        /// <summary>
        /// Ruft den Manager ab, der für die Verwaltung der Anwendungssprachen zuständig ist.
        /// </summary>
        public AppSprachenManager Sprachen => GetManager<AppSprachenManager>();

        #endregion Anwendungsobjekt-Fabrik
    }
}
