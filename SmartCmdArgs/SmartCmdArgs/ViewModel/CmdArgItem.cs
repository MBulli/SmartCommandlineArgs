using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.Helper;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgItem : PropertyChangedBase
    {
        private Guid id;
        private bool enabled;
        private string project;       
        private string command;

        public Guid Id
        {
            get { return id; }
            set { id = value; OnNotifyPropertyChanged(); }
        }

        public string Command
        {
            get { return command; }
            set { this.command = value; OnNotifyPropertyChanged(); }
        }

        [Obsolete]
        public string Project
        {
            get { return project; }
            set { this.project = value; OnNotifyPropertyChanged(); }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; OnNotifyPropertyChanged(); }
        }
    }

    public struct CmdArgClipboardItem
    {
        public bool Enabled { get; set; }
        public string Command { get; set; }
    }
}
