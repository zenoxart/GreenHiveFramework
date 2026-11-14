namespace GreenHive.Wpf.Controls.Common.TabView
{
    /// <summary>
    /// Event arguments for when a tab item is removed
    /// </summary>
    public class TabItemRemovedEventArgs : EventArgs
    {
        public Core.ExtDto.TabItem RemovedTabItem { get; }

        public TabItemRemovedEventArgs(Core.ExtDto.TabItem removedTabItem)
        {
            RemovedTabItem = removedTabItem;
        }
    }
}
