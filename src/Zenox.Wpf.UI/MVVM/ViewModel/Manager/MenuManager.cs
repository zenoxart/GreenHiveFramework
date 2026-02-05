using System.Windows;
using System.Windows.Media;
using Zenox.Wpf.Controls.Common.CommandHistory;
using Zenox.Wpf.Controls.Common.CommandPalette;
using Zenox.Wpf.Core.Common.Command;
using Zenox.Wpf.Views;
using Zenox.Wpf.Views.Hilfe;

namespace Zenox.Wpf.UI.MVVM.ViewModel.Manager
{
	public class MenuManager : BaseManager
	{
		private RelayMenu _topMenu;
		private RelayMenu _pageMenu;
		private RelayMenu _bottomMenu;
		private RelayMenu _footerMenu;

		public MenuManager()
		{
			InitializeCommandPalette();
		}

		/// <summary>
		/// Initialisiert die Command Palette automatisch aus den Menü-Strukturen
		/// </summary>
		private void InitializeCommandPalette()
		{
			var paletteManager = CommandPaletteManager.Instance;

			RegisterMenuCommands(TopMenu, "Navigation");
			RegisterMenuCommands(BottomMenu, "System");

			paletteManager.RegisterCommand(
				"Befehlspalette öffnen",
				"System",
				() => CommandPaletteManager.Instance.ShowPalette(),
				"Öffnet die Befehlspalette",
				"Ctrl+K",
				keywords: new[] { "befehl", "command", "palette", "suche", "search" }
			);
		}

		/// <summary>
		/// Registriert alle Commands aus einem Menü rekursiv in der Command Palette
		/// </summary>
		/// <param name="menu">Das Menü, dessen Commands registriert werden sollen</param>
		/// <param name="defaultCategory">Die Standard-Kategorie für Commands ohne übergeordnetes Menü</param>
		private void RegisterMenuCommands(RelayMenu menu, string defaultCategory)
		{
			if (menu == null) return;

			var paletteManager = CommandPaletteManager.Instance;

			foreach (var item in menu)
			{
				RegisterRelayObject(item, defaultCategory, paletteManager);
			}
		}

		/// <summary>
		/// Registriert ein einzelnes RelayObject und seine Kinder rekursiv
		/// </summary>
		private void RegisterRelayObject(RelayObject item, string category, CommandPaletteManager paletteManager)
		{
			if (item == null || string.IsNullOrWhiteSpace(item.Name)) return;

			if (item.Command != null && item.Command is RelayCommand relayCommand)
			{
				var keywords = GenerateKeywords(item.Name);
				
				paletteManager.RegisterCommand(
					item.Name,
					category,
					() => relayCommand.Execute(null),
					$"Führt '{item.Name}' aus",
					keywords: keywords
				);
			}

			if (item.Children != null && item.Children.Count > 0)
			{
				var childCategory = item.Name;
				foreach (var child in item.Children)
				{
					RegisterRelayObject(child, childCategory, paletteManager);
				}
			}
		}

		/// <summary>
		/// Generiert Keywords für die Suche basierend auf dem Namen
		/// </summary>
		private string[] GenerateKeywords(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return System.Array.Empty<string>();

			var keywords = new System.Collections.Generic.List<string>();
			
			var lowerName = name.ToLower();
			keywords.Add(lowerName);
			
			var words = name.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
			foreach (var word in words)
			{
				keywords.Add(word.ToLower());
			}

			return keywords.ToArray();
		}

		public RelayMenu TopMenu
		{
			get
			{
				if (_topMenu == null)
				{
					_topMenu = new RelayMenu();

					_topMenu.Add(new RelayObject
					{
						Name = "Einstellungen",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
							this.AppKontext.TabView.LoadView<EinstellungenView>();
						}, _ => true),
						Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
							new() {
								Name = "Performancepanel",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									Controls.Common.ExecutionLog.ExecutionLogFenster exelogWindow = new();
									exelogWindow.ExecutionLogList = CommandPool.ExecutionLog;
									exelogWindow.Show();
								}, _ => true)
							},
							new() {
								Name = "Anzeigeeinstellungen",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
								}, _ => true)
							},
							new() {
								Name = "Anwendungseinstellungen",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									this.AppKontext.TabView.LoadView<EinstellungenView>();
								}, _ => true)
							},

						}
					});

					_topMenu.Add(new RelayObject
					{
						Name = "Benutzer",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
						}, _ => true),
						Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
							new() {

								Name = "Profil",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									this.AppKontext.TabView.LoadView<ProfilView>();
								}, _ => true)
							},
							new() {

								Name = "Abmelden",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									// Token löschen
									Properties.Settings.Default.AuthToken = string.Empty;
									Properties.Settings.Default.Save();
									
									// Anwendung neu starten
									System.Diagnostics.Process.Start(
										System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!);
									System.Windows.Application.Current.Shutdown();
								}, _ => true)
							},

						}
					});

					_topMenu.Add(new RelayObject
					{
						Name = "Hilfe",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
						}, _ => true),
						Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
							new() {

								Name = "Hilfe Anzeigen",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									MainWindowManager.StartTutorialCommand.Execute("MainWindow");
								}, _ => true)
							},
							new() {

								Name = "Glossar",
								Foreground = Brushes.White,
					Command = new RelayCommand(
					_ =>
					{
						this.AppKontext.TabView.LoadView<GlossarView>();
					}, _ => true)
							},
							new() {

								Name = "Neuigkeiten",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									this.AppKontext.TabView.LoadView<NeuigkeitenView>();
								}, _ => true)
							},
							new() {

								Name = "Feedback senden",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
								}, _ => true),
								Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
									new() {

										Name = "Problem melden",
										Foreground = Brushes.White,
										Command = new RelayCommand(
										_ =>
										{
										}, _ => true)
									},
									new() {

										Name = "Feature vorschlagen",
										Foreground = Brushes.White,
										Command = new RelayCommand(
										_ =>
										{
										}, _ => true)
									}
								},
							},
							new() {

								Name = "About",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									this.AppKontext.TabView.LoadView<AboutView>();
								}, _ => true)
							},

						}
					});

				}
				return _topMenu;
			}
		}

		public RelayMenu PageMenu
		{
			get
			{
				if (_pageMenu == null)
				{
					_pageMenu = new RelayMenu();

					// Dashboard
					_pageMenu.Add(new RelayObject
					{
						PngPath = "/Assets/arrow-right-02-stroke-rounded.png",
						Name = "Dashboard",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
							this.AppKontext.TabView.LoadView<DashboardView>();
						}, _ => true)
					});

					

					

					
				}
				return _pageMenu;
			}
		}

		public RelayMenu BottomMenu
		{
			get
			{
				if (_bottomMenu == null)
				{
					_bottomMenu = new RelayMenu();
					_bottomMenu.Add(new RelayObject
					{
						Name = "Anwendungs-Logs",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
							MessageBox.Show("Anwendungs-Logs!");
						}, _ => true)
					});
				}
				return _bottomMenu;
			}
		}

		public RelayMenu FooterMenü
		{
			get
			{
				if (_footerMenu == null)
				{
					var relayCommandPool = new RelayCommandPool();
					// Footer-Menu Implementation falls benötigt
				}
				return _footerMenu;
			}
			set { SetProperty(ref _footerMenu, value); }
		}
	}
}
