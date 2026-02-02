using Zenox.Wpf.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.UI.Common
{
    public class UINotificationObject : NotificationObject
    {
        public new virtual UIAppKontext AppKontext
        {
            get
            {
                return base.AppKontext as UIAppKontext;
            }
            set
            {
                base.AppKontext = value;
            }
        }

    }
}
