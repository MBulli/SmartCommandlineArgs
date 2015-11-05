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

        private RelayCommand addEntryCommand;
        public RelayCommand AddEntryCommand { get { return addEntryCommand; } }


        private RelayCommand<CmdArgItem> removeEntryCommand;
        public RelayCommand<CmdArgItem> RemoveEntryCommand { get { return removeEntryCommand; } }

        public CmdArgsToolWindowViewModel()
        {
            this.CommandlineArguments = new CmdArgListViewModel();
            this.StartupProject = CmdArgStorage.Instance.StartupProject;

            CmdArgStorage.Instance.StartupProjectChanged += Instance_StartupProjectChanged;

            addEntryCommand = new RelayCommand(
                () => {
                    var newItem = CmdArgStorage.Instance.AddEntry(command: "", enabled: true);
                    CommandlineArguments.AddCmdArgStoreEntry(newItem);
                }, canExecute: _ =>
                {
                    return this.StartupProject != null;
                });

            removeEntryCommand = new RelayCommand<CmdArgItem>(
               item => {
                   if (item != null)
                   {
                       CmdArgStorage.Instance.RemoveEntryById(item.Id);
                       CommandlineArguments.RemoveById(item.Id);
                   }
               }, canExecute: _ =>
               {
                   return this.StartupProject != null;
               });
        }

        public void UpdateView()
        {
            this.StartupProject = CmdArgStorage.Instance.StartupProject;

            if (StartupProject != null)
            {
                this.CommandlineArguments.SetListItems(CmdArgStorage.Instance.StartupProjectEntries);
            }
        }

        private void Instance_StartupProjectChanged(object sender, EventArgs e)
        {
            UpdateView();
        }
    }
}
