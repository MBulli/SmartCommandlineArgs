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

        private Lazy<RelayCommand> addEntryCommand;
        public RelayCommand AddEntryCommand { get { return addEntryCommand.Value; } }


        private Lazy<RelayCommand<CmdArgItem>> removeEntryCommand;
        public RelayCommand<CmdArgItem> RemoveEntryCommand { get { return removeEntryCommand.Value; } }

        public CmdArgsToolWindowViewModel()
        {
            this.CommandlineArguments = new CmdArgListViewModel();

            CmdArgStorage.Instance.StartupProjectChanged += Instance_StartupProjectChanged;

            addEntryCommand = new Lazy<RelayCommand>(
            () => new RelayCommand(
                () => {
                    CmdArgStorage.Instance.AddEntry(command: "", enabled: true);
                }, canExecute: _ =>
                {
                    return this.StartupProject != null;
                }));

            removeEntryCommand = new Lazy<RelayCommand<CmdArgItem>>(
            () => new RelayCommand<CmdArgItem>(
               item => {
                   if (item != null)
                       CmdArgStorage.Instance.RemoveEntryById(item.Id);
               }, canExecute: _ =>
               {
                   return this.StartupProject != null;
               }));
        }

        private void Instance_StartupProjectChanged(object sender, EventArgs e)
        {
            this.StartupProject = CmdArgStorage.Instance.CurStartupProject;
        }
    }
}
