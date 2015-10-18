using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartCmdArgs.Model;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgsToolWindowViewModel : PropertyChangedBase
    {
        public CmdArgListViewModel CommandlineArguments { get; private set; }

        public CmdArgsToolWindowViewModel()
        {
            this.CommandlineArguments = new CmdArgListViewModel();

            this.CommandlineArguments.CmdLineItems.CollectionChanged += CmdLineItems_CollectionChanged;

            foreach (CmdArgStorageEntry entry in CmdArgStorage.Instance.Entries)
            {
                CommandlineArguments.CmdLineItems.Add(new CmdArgItem(entry.Enabled, entry.Command));
            }
        }

        private void CmdLineItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            
        }
    }
}
