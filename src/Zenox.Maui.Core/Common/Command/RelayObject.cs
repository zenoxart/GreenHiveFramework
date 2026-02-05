using System.Collections.ObjectModel;

namespace Zenox.NET.Core.Common.Command
{
	/// <summary>
	/// Stellt ein Relay-Baum-Knoten-Objekt dar, das Befehle und andere RelayObjects wie ganze Pools enthalten kann.
	/// Diese Klasse ermöglicht die Erstellung hierarchischer Befehlsstrukturen für komplexe Menü- und Navigationssysteme.
	/// </summary>
	/// <remarks>
	/// RelayObject erweitert NotificationObject und bietet eine Baumstruktur für die Organisation von Befehlen.
	/// Jedes RelayObject kann einen Namen, einen zugehörigen Befehl, untergeordnete Elemente und einen Verweis 
	/// auf das übergeordnete Element haben. Die Klasse synchronisiert automatisch die Namen zwischen dem RelayObject 
	/// und dem zugewiesenen Befehl, um Konsistenz zu gewährleisten.
	/// </remarks>
	public class RelayObject : MVVM.ViewModelSupport.NotificationObject
	{
		private string? _name;
		private RelayCommand? _command;
		private ObservableCollection<RelayObject>? _children;
		private RelayObject? _parent;
		private string? _pngPath;

		/// <summary>
		/// Initialisiert eine neue Instanz der RelayObject-Klasse.
		/// </summary>
		/// <remarks>
		/// Der Konstruktor erstellt automatisch eine leere ObservableCollection für untergeordnete Elemente.
		/// </remarks>
		public RelayObject()
		{
			Children = new ObservableCollection<RelayObject>();
		}

		/// <summary>
		/// Ruft den Namen des RelayObjects ab oder legt ihn fest.
		/// </summary>
		/// <value>Der Anzeigename des RelayObjects, der in der Benutzeroberfläche verwendet wird.</value>
		/// <remarks>
		/// Beim Setzen des Namens wird automatisch der Name des zugewiesenen Befehls synchronisiert,
		/// falls ein Befehl vorhanden ist. Dies gewährleistet Konsistenz zwischen RelayObject und Befehl.
		/// </remarks>
		public string? Name
		{
			get => _name;
			set
			{
				_name = value;
				// Namen des Befehls synchronisieren, falls ein Befehl zugewiesen ist
				if (Command != null)
				{
					Command.Name = value;
				}
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Ruft den diesem RelayObject zugewiesenen Befehl ab oder legt ihn fest.
		/// </summary>
		/// <value>Der RelayCommand, der ausgeführt wird, wenn dieses RelayObject aktiviert wird, oder null wenn kein Befehl zugewiesen ist.</value>
		/// <remarks>
		/// Beim Setzen eines Befehls wird automatisch der Befehlsname mit dem Namen des RelayObjects synchronisiert,
		/// falls beide Werte verfügbar sind. Dies stellt sicher, dass die Namen konsistent bleiben.
		/// </remarks>
		public RelayCommand? Command
		{
			get => _command;
			set
			{
				_command = value;
				// Befehlsnamen synchronisieren, falls sowohl Name als auch Befehl verfügbar sind
				if (value != null && !string.IsNullOrEmpty(Name))
				{
					value.Name = Name;
				}
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Ruft die Sammlung der untergeordneten RelayObject-Elemente ab oder legt sie fest.
		/// </summary>
		/// <value>Eine ObservableCollection von RelayObject-Elementen, die die Kinder dieses Objekts darstellen, oder null wenn keine Kinder vorhanden sind.</value>
		/// <remarks>
		/// Diese Eigenschaft ermöglicht die Erstellung hierarchischer Strukturen, bei denen jedes RelayObject
		/// weitere RelayObjects als Kinder haben kann. Die ObservableCollection stellt sicher, dass UI-Bindungen
		/// automatisch über Änderungen in der Kindersammlung benachrichtigt werden.
		/// </remarks>
		public ObservableCollection<RelayObject>? Children
		{
			get => _children;
			set
			{
				_children = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Ruft das übergeordnete RelayObject ab oder legt es fest.
		/// </summary>
		/// <value>Das RelayObject, das als Elternelement fungiert, oder null wenn dies ein Wurzelelement ist.</value>
		/// <remarks>
		/// Diese Eigenschaft ermöglicht die Navigation in der Baumstruktur nach oben und die Bestimmung
		/// der hierarchischen Position eines RelayObjects innerhalb der Gesamtstruktur.
		/// </remarks>
		public RelayObject? Parent
		{
			get => _parent;
			set
			{
				_parent = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Optionaler Png-Path für die grafische Darstellung im Menü.
		/// </summary>
		public string? PngPath
		{
			get => _pngPath;
			set
			{
				if (_pngPath != value)
				{
					_pngPath = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(PngResourcePath));
				}
			}
		}

		/// <summary>
		/// Gibt den WPF-Ressourcenpfad für die Verwendung im Image-Source zurück.
		/// </summary>
		public string? PngResourcePath
		{
			get
			{
				if (string.IsNullOrEmpty(PngPath))
					return null;

				// Annahme: Die Assembly heißt "Zenox.Wpf.Controls" und die Assets liegen im Unterordner "Assets"
				if (PngPath.StartsWith("/"))
					return $"/Voestalpine.Wpf.Controls;component{PngPath}";
				else
					return $"/Voestalpine.Wpf.Controls;component/{PngPath}";
			}
		}
	}
}

