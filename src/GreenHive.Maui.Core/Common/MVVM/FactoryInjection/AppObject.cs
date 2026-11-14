

namespace GreenHive.NET.Core.Common.MVVM.FactoryInjection
{
	/// <summary>
	/// Stellt die Basisklasse für Anwendungsobjekte dar, 
	/// die grundlegende Funktionalitäten wie Berechtigungsmanagement 
	/// und Fehlerbehandlung bereitstellt.
	/// </summary>
	public class AppObject : IAppObject
	{


		

		public AppObject()
		{
		}

		/// <summary>
		/// Stellt den Kontext der Anwendung bereit.
		/// </summary>
		public AppKontext AppKontext { get; set; } = null!;

		/// <summary>
		/// Wird ausgelöst, wenn ein Problem 
		/// aufgetreten ist
		/// </summary>
		/// <remarks>Die Ursache befindet sich
		/// in den Ereignisdaten</remarks>
		public event FehlerAufgetretenEventHandler? FehlerAufgetreten;

		/// <summary>
		/// Löst das Ereignis FehlerAufgetreten aus
		/// </summary>
		/// <param name="e">Ereignisdaten</param>
		public virtual void OnFehlerAufgetreten(
						FehlerAufgetretenEventArgs e)
		{
			// Damit die Garbage Collection
			// das Objekt nicht entfernt, mit
			// einer Kopie arbeiten
			var BehandlerKopie = this.FehlerAufgetreten;

			BehandlerKopie?.Invoke(this, e);
		}


		void IAppObject.OnFehlerAufgetreten(FehlerAufgetretenEventArgs e)
		{
			OnFehlerAufgetreten(e);
		}

		#region Protokollierung ...
		protected virtual void StartMelden([System.Runtime.CompilerServices.CallerMemberName] string aufrufer = null!)
		{
			if (this.Logger.Stufe == LogStufe.Alles)
			{
				var Besitzer = (new System.Diagnostics.StackTrace(skipFrames: 1))
						.GetFrame(0)!
						.GetMethod()!
						.DeclaringType!.FullName;
				this.Logger.Eintragen($"{Besitzer}.{aufrufer} startet...");
			}
		}
		protected virtual void EndeMelden([System.Runtime.CompilerServices.CallerMemberName] string aufrufer = null!)
		{
			if (this.Logger.Stufe == LogStufe.Alles)
			{
				var Besitzer = (new System.Diagnostics.StackTrace(skipFrames: 1))
						.GetFrame(0)!
						.GetMethod()!
						.DeclaringType!.FullName;
				this.Logger.Eintragen($"{Besitzer}.{aufrufer} beendet.");
			}
		}

		void IAppObject.StartMelden(string aufrufer)
		{
			StartMelden(aufrufer);
		}

		void IAppObject.EndeMelden(string aufrufer)
		{
			EndeMelden(aufrufer);
		}

		public AppLogManager _log;

		/// <summary>
		/// Ruft den Anwendungsprotokoll-Manager ab, der für die Verwaltung und den Zugriff auf protokollbezogene Funktionalitäten verwendet wird.
		/// </summary>
		public AppLogManager Logger
		{
			get
			{
				if (this.AppKontext != null)
				{
					this._log = this.AppKontext.Produziere<AppLogManager>();
				}
				return _log;
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value), "Der Logger darf nicht null sein.");
				this._log = value;
			}
		}


		#endregion Protokollierung ...
	}
}
