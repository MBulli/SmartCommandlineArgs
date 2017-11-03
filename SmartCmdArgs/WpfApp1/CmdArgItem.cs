using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class CmdProject : PropertyChangedBase
    {
        private ObservableCollection<CmdItem> items;
        private string name;

        public ObservableCollection<CmdItem> Items { get => items; set => SetAndNotify(value, ref items); }
        public string Name { get => name; set => SetAndNotify(value, ref name); }
    }

    public class CmdItem : PropertyChangedBase
    {
        private bool isEditing;

        public bool IsEditing { get => isEditing; set => SetAndNotify(value, ref isEditing); }
    }

    public class CmdGroup : CmdItem
    {
        private ObservableCollection<CmdItem> items;
        private string name;

        public ObservableCollection<CmdItem> Items { get => items; set => SetAndNotify(value, ref items); }
        public string Name { get => name; set => SetAndNotify(value, ref name); }
    }

    public class CmdArgument : CmdItem
    {
        private bool enabled;
        private string command;

        public bool Enabled { get => enabled; set => SetAndNotify(value, ref enabled); }
        public string Command { get => command; set => SetAndNotify(value, ref command); }
    }


    public class PropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetAndNotify<T>(T newValue, ref T field, [CallerMemberName]string propertyName = null)
        {
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void OnNotifyPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
