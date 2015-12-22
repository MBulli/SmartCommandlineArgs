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

        public RelayCommand AddEntryCommand { get; }

        public RelayCommand<IList> RemoveEntriesCommand { get; }

        public RelayCommand<IList> MoveEntriesUpCommand { get; }

        public RelayCommand<IList> MoveEntriesDownCommand { get; }

        public ToolWindowViewModel()
        {
            this.CommandlineArguments = new ListViewModel();

            AddEntryCommand = new RelayCommand(
                () => {
                    CommandlineArguments.AddNewItem(command: "", project: StartupProject, enabled: true);
                }, canExecute: _ =>
                {
                    return StartupProject != null;
                });

            RemoveEntriesCommand = new RelayCommand<IList>(
               items => {
                   if (items != null && items.Count != 0)
                   {
                       CommandlineArguments.RemoveEntries(items.Cast<CmdArgItem>());
                   }
               }, canExecute: _ =>
               {
                   return StartupProject != null;
               });

            MoveEntriesUpCommand = new RelayCommand<IList>(
               items => {
                   if (items != null && items.Count != 0)
                   {
                       CommandlineArguments.MoveEntriesUp(items.Cast<CmdArgItem>());
                   }
               }, canExecute: _ =>
               {
                   return this.StartupProject != null;
               });

            MoveEntriesDownCommand = new RelayCommand<IList>(
               items =>
               {
                   if (items != null && items.Count != 0)
                   {
                       CommandlineArguments.MoveEntriesDown(items.Cast<CmdArgItem>());
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
