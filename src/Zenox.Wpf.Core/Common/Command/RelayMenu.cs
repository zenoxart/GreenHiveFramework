using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Zenox.Wpf.Core.Common.Command
{
    /// <summary>
    /// Stellt ein Menü dar, das eine Sammlung von RelayObject-Elementen enthält und Eigenschaftsbenachrichtigungen unterstützt.
    /// Diese Klasse wird für die Erstellung hierarchischer Menüstrukturen in WPF-Anwendungen verwendet.
    /// </summary>
    /// <remarks>
    /// RelayMenu erweitert ObservableCollection&lt;RelayObject&gt; und implementiert INotifyPropertyChanged,
    /// um eine vollständige Datenbindungsunterstützung für WPF-UI-Elemente zu bieten.
    /// Jedes Menü kann einen Namen haben und mehrere RelayObject-Elemente enthalten, die Befehle oder Untermenüs darstellen können.
    /// </remarks>
    public class RelayMenu : ObservableCollection<RelayObject>, System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>
        /// Tritt auf, wenn sich eine Eigenschaft ändert.
        /// </summary>
        public new event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Löst das PropertyChanged-Ereignis aus, um Listener über Eigenschaftsänderungen zu benachrichtigen.
        /// </summary>
        /// <param name="eigenschaft">Der Name der geänderten Eigenschaft. Wird automatisch durch CallerMemberName bereitgestellt.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string eigenschaft = "")
        {
            this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(eigenschaft));
        }

        /// <summary>
        /// Ruft den Namen des Menüs ab oder legt ihn fest.
        /// </summary>
        /// <value>Der Anzeigename des Menüs, der in der Benutzeroberfläche verwendet wird.</value>
        public string Name
        {
            get { return field; }
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    /// <summary>
    /// Stellt eine Gruppe von RelayMenu-Objekten dar und unterstützt Eigenschaftsbenachrichtigungen.
    /// Diese Klasse wird verwendet, um mehrere Menüs zu einer logischen Gruppe zusammenzufassen.
    /// </summary>
    /// <remarks>
    /// RelayMenuGroup erweitert ObservableCollection&lt;RelayMenu&gt; und implementiert INotifyPropertyChanged,
    /// um eine vollständige Datenbindungsunterstützung für WPF-UI-Elemente zu bieten.
    /// Sie ermöglicht die Erstellung komplexer Menüstrukturen mit mehreren Ebenen und Kategorien.
    /// </remarks>
    public class RelayMenuGroup : ObservableCollection<RelayMenu>, System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>
        /// Tritt auf, wenn sich eine Eigenschaft ändert.
        /// </summary>
        public new event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Löst das PropertyChanged-Ereignis aus, um Listener über Eigenschaftsänderungen zu benachrichtigen.
        /// </summary>
        /// <param name="eigenschaft">Der Name der geänderten Eigenschaft. Wird automatisch durch CallerMemberName bereitgestellt.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string eigenschaft = "")
        {
            this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(eigenschaft));
        }

        /// <summary>
        /// Ruft den Namen der Menügruppe ab oder legt ihn fest.
        /// </summary>
        /// <value>Der Anzeigename der Menügruppe, der zur Kategorisierung und Anzeige verwendet wird.</value>
        public string Name
        {
            get { return field; }
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}