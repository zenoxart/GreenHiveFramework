using System.Collections.ObjectModel;
using System.Windows.Input;
using Zenox.NET.Core.Common.Command;

namespace Zenox.Wpf.UI.MVVM.ViewModel.Manager
{
    public class AdminViewManager : BaseManager
    {
        private string _status = "Bereit";
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> Logs { get; } = new();

        public ICommand AdminActionCommand { get; }

        public AdminViewManager()
        {
            AdminActionCommand = new RelayCommand(_ => ExecuteAdminAction());
        }

        private void ExecuteAdminAction()
        {
            Status = "Admin-Aktion ausgeführt";
            Logs.Add("Admin-Aktion wurde ausgeführt.");
        }

        private RelayMenu _AdminMenu;
        public RelayMenu AdminMenu
        {
            get
            {
                if (_AdminMenu == null)
                {
                    _AdminMenu = new RelayMenu();


                    _AdminMenu.Add(new RelayObject
                    {
                        Name = "Admin 1",
                        Command = new RelayCommand(
                        _ =>
                        {

                        }, _ => true)
                    });

                    _AdminMenu.Add(new RelayObject
                    {
                        Name = "Admin 2",
                        Command = new RelayCommand(
                        _ =>
                        {

                        }, _ => true)
                    });

                    _AdminMenu.Add(new RelayObject
                    {
                        Name = "Admin 3",
                        Command = new RelayCommand(
                        _ =>
                        {

                        }, _ => true)
                    });
                }

                return _AdminMenu;
            }
        }
    }
}