using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Model;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgsToolWindowViewModel : PropertyChangedBase
    {
        public CmdArgListViewModel CommandlineArguments { get; private set; }

        private string startupProject;
        public string StartupProject
        {
            get { return startupProject; }
            private set { startupProject = value; OnNotifyPropertyChanged(); }
        }

        public RelayCommand AddEntryCommand { get { return addEntryCommand.Value; } }
        private Lazy<RelayCommand> addEntryCommand = new Lazy<RelayCommand>(
            () => new RelayCommand(
                () =>
                {
                    if (CmdArgStorage.Instance.CurStartupProject != null)
                        CmdArgStorage.Instance.AddEntry(command: "", enabled: true);
                    else
                        MessageBox.Show("No startup project found!", "No startup project", MessageBoxButton.OK, MessageBoxImage.Warning);
                })); 

        public RelayCommand<CmdArgItem> RemoveEntryCommand { get { return removeEntryCommand.Value; } }
        private Lazy<RelayCommand<CmdArgItem>> removeEntryCommand = new Lazy<RelayCommand<CmdArgItem>>(
            () => new RelayCommand<CmdArgItem>(
                item => { if (item != null) CmdArgStorage.Instance.RemoveEntryById(item.Id); })); 

        public CmdArgsToolWindowViewModel()
        {
            this.CommandlineArguments = new CmdArgListViewModel();

            CmdArgStorage.Instance.StartupProjectChanged += Instance_StartupProjectChanged;
        }

        private void Instance_StartupProjectChanged(object sender, EventArgs e)
        {
            this.StartupProject = CmdArgStorage.Instance.CurStartupProject;
        }
    }
}
