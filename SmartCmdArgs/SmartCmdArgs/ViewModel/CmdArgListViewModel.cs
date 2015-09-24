using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgListViewModel : PropertyChangedBase
    {
        public ObservableCollection<CmdArgItem> CmdLineItems { get; private set; }

        public CmdArgListViewModel()
        {
            this.CmdLineItems = new ObservableCollection<CmdArgItem>();
            this.CmdLineItems.Add(new CmdArgItem(true, "Hallo"));
            this.CmdLineItems.Add(new CmdArgItem(false, "Welt"));
        }
    }
}
