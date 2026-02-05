using System.Windows;
using System.Windows.Controls;
using Zenox.NET.Core.Common.Command;

namespace Zenox.Wpf.Controls.Common.Menu
{
    /// <summary>
    /// Interaktionslogik für TopMenuControl.xaml
    /// </summary>
    public partial class TopMenuControl : UserControl
    {
        public TopMenuControl()
        {
            InitializeComponent();
        }

        public RelayMenu MyTopMenu
        {
            get { return (RelayMenu)GetValue(MyTopMenuProperty); }
            set { SetValue(MyTopMenuProperty, value); }
        }

        public static readonly DependencyProperty MyTopMenuProperty =
            DependencyProperty.Register("MyTopMenu", typeof(RelayMenu), typeof(TopMenuControl), new PropertyMetadata(null));

    }
}
