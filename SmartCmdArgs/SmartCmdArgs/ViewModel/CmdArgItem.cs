using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgItem : PropertyChangedBase
    {
        private bool enabled;
        private string value;

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

        public CmdArgItem(bool enabled = false, string value = "")
        {
            this.enabled = enabled;
            this.value = value;
        }
    }
}
