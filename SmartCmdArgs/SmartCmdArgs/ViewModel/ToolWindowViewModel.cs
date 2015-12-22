using System;
using System.Collections;
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
    public class ToolWindowViewModel : PropertyChangedBase
    {
        public ListViewModel CommandlineArguments { get; private set; }

        private string startupProject;
        public string StartupProject
        {
            get { return startupProject; }
            private set { startupProject = value; OnNotifyPropertyChanged(); }
        }

        private RelayCommand addEntryCommand;
        public RelayCommand AddEntryCommand { get { return addEntryCommand; } }


        private RelayCommand<IList> removeEntriesCommand;
        public RelayCommand<IList> RemoveEntriesCommand { get { return removeEntriesCommand; } }


        private RelayCommand<IList> moveEntriesUpCommand;
        public RelayCommand<IList> MoveEntriesUpCommand { get { return moveEntriesUpCommand; } }


        private RelayCommand<IList> moveEntriesDownCommand;
        public RelayCommand<IList> MoveEntriesDownCommand { get { return moveEntriesDownCommand; } }

        public ToolWindowViewModel()
        {
            this.CommandlineArguments = new ListViewModel();

            addEntryCommand = new RelayCommand(
                () => {
                    CommandlineArguments.AddNewItem(command: "", project: StartupProject, enabled: true);
                }, canExecute: _ =>
                {
                    return StartupProject != null;
                });

            removeEntriesCommand = new RelayCommand<IList>(
               items => {
                   if (items != null && items.Count != 0)
                   {
                       foreach (var id in items.Cast<CmdArgItem>().Select(arg => arg.Id).ToList())
                       {
                           CommandlineArguments.RemoveById(id);
                       }
                   }
               }, canExecute: _ =>
               {
                   return StartupProject != null;
               });

            moveEntriesUpCommand = new RelayCommand<IList>(
               items => {
                   if (items != null && items.Count != 0)
                   {
                       foreach (var id in items.Cast<CmdArgItem>().Select(arg => arg.Id).ToList())
                       {
                           CommandlineArguments.MoveEntryUp(id);
                       }
                   }
               }, canExecute: _ =>
               {
                   return this.StartupProject != null;
               });

            moveEntriesDownCommand = new RelayCommand<IList>(
               items =>
               {
                   if (items != null && items.Count != 0)
                   {
                       foreach (var id in items.Cast<CmdArgItem>().Select(arg => arg.Id).Reverse().ToList())
                       {
                           CommandlineArguments.MoveEntryDown(id);
                       }
                   }
               }, canExecute: _ =>
               {
                   return this.StartupProject != null;
               });
        }

        public IEnumerable<CmdArgItem> ActiveItemsForCurrentProject()
        {
            foreach (CmdArgItem item in CommandlineArguments.CmdLineItems.SourceCollection)
            {
                if (item.Enabled && item.Project == StartupProject)
                {
                    yield return item;
                }
            }
        }

        public void UpdateStartupProject(string projectName)
        {
            if (StartupProject != projectName)
            {
                this.StartupProject = projectName;
                CommandlineArguments.FilterByProject(projectName);
            }
        }
    }
}
