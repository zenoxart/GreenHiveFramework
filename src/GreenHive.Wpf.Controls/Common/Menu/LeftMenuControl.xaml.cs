using System.Windows;
using System.Windows.Controls;
using GreenHive.NET.Core.Common.Command;

namespace GreenHive.Wpf.Controls.Common.Menu
{
    /// <summary>
    /// Interaktionslogik für LeftMenuControl.xaml
    /// </summary>
    public partial class LeftMenuControl : UserControl
    {
        public LeftMenuControl()
        {
            InitializeComponent();
        }
        public RelayMenu MyPageLeftMenu
        {
            get { return (RelayMenu)GetValue(MyPageLeftMenuProperty); }
            set { SetValue(MyPageLeftMenuProperty, value); }
        }

        public static readonly DependencyProperty MyPageLeftMenuProperty =
            DependencyProperty.Register("MyPageLeftMenu", typeof(RelayMenu), typeof(LeftMenuControl), new PropertyMetadata(null));
    }
}
