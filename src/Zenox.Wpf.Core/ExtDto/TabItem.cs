using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Controls;
using Zenox.NET.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.Core.ExtDto
{

    public class TabItem : NotificationObject
    {
        private string _name;
        private UserControl _content;
        private bool _isPinned;

        public string Name 
        { 
            get => _name; 
            set => SetProperty(ref _name, value); 
        }

        public UserControl Content 
        { 
            get => _content; 
            set => SetProperty(ref _content, value); 
        }

        public bool IsPinned 
        { 
            get => _isPinned; 
            set => SetProperty(ref _isPinned, value); 
        }
    }

    public class TabItems : ObservableCollection<TabItem> { }

    public class UserControls : ObservableCollection<UserControl> { }


}
