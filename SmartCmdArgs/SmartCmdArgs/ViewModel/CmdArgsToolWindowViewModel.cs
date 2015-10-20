using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IramesYTD.Utils;
using SmartCmdArgs.Model;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgsToolWindowViewModel : PropertyChangedBase
    {
        public CmdArgListViewModel CommandlineArguments { get; private set; }

        public RelayCommand AddEntryCommand { get { return addEntryCommand.Value; } }
        private Lazy<RelayCommand> addEntryCommand = new Lazy<RelayCommand>(
            () => new RelayCommand(
                () => {
                    // TODO: add warning message
                    if (CmdArgStorage.Instance.CurStartupProject != null)
                        CmdArgStorage.Instance.AddEntry(command: "", enabled: true);
                })); 

        public RelayCommand<CmdArgItem> RemoveEntryCommand { get { return removeEntryCommand.Value; } }
        private Lazy<RelayCommand<CmdArgItem>> removeEntryCommand = new Lazy<RelayCommand<CmdArgItem>>(
            () => new RelayCommand<CmdArgItem>(
                item => CmdArgStorage.Instance.RemoveEntryById(item.Id))); 

        public CmdArgsToolWindowViewModel()
        {
            this.CommandlineArguments = new CmdArgListViewModel();
        }
    }
}
