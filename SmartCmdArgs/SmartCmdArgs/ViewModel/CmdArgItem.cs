using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgItem : PropertyChangedBase
    {
        private Guid id;
        private bool enabled;
        private string value;

        public Guid Id
        {
            get { return id; }
            set { id = value; OnNotifyPropertyChanged(); }
        }

        public string Value
        {
            get { return value; }
            set { this.value = value; OnNotifyPropertyChanged(); }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; OnNotifyPropertyChanged(); }
        }
    }
}
