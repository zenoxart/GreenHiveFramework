namespace Zenox.NET.Core.Common.Command.Model
{
    public class MenuItemRecord
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string IconName { get; set; }
        public RelayCommand Command { get; set; }
    }
}
