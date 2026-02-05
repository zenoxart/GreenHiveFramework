using System;
using System.Collections.Generic;
using System.Text;
using Zenox.NET.Core.Common.MVVM.FactoryInjection;
using Zenox.NET.Core.Common.Permission;

namespace Zenox.Wpf.Advanced.MVVM.FactoryInjection
{
    public class WPFAppKontext : AppKontext
    {/// <summary>
     /// Erzeugt eine neue Instanz des angegebenen Anwendungsobjekts.
     /// </summary>
     /// <typeparam name="T">Der Typ der zu Instanzieren ist</typeparam>
     /// <returns></returns>
        public virtual T Produziere<T>(PermissionType Berechtigung)
            where T : WPFAppObject, new()
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
                Logger.Eintragen($"==> {NeuesObjekt} produziert und initialisiert...");
            }
            NeuesObjekt.FehlerAufgetreten
                += (sender, e)
                => this.Logger.Eintragen(
                    $"==> FEHLER! {sender} Ausnahme \"{e.Ursache.Message}\"",
                    LogEintragTyp.Fehler);

            // Hier weitere Initalisierungen ergänzen

            return NeuesObjekt;
        }
    }
}
