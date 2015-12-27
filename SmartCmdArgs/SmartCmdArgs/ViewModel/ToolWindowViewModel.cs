using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.Helper;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.ViewModel
{
    public class ToolWindowViewModel : PropertyChangedBase
    {
        private Dictionary<string, ListViewModel> solutionArguments; 

        private ListViewModel _currentArgumentList;
        public ListViewModel CurrentArgumentList
        {
            get { return _currentArgumentList; }
            set { _currentArgumentList = value; OnNotifyPropertyChanged(); }
        }

        private string _startupProject;
        public string StartupProject
        {
            get { return _startupProject; }
            private set { _startupProject = value; OnNotifyPropertyChanged(); }
        }

        public RelayCommand AddEntryCommand { get; }

        public RelayCommand RemoveEntriesCommand { get; }

        public RelayCommand MoveEntriesUpCommand { get; }

        public RelayCommand MoveEntriesDownCommand { get; }

        public RelayCommand ToogleItemsEnabledCommand { get; }

        public event EventHandler CommandLineChanged;
        public event EventHandler<IList> SelectedItemsChanged;

        public ToolWindowViewModel()
        {
            solutionArguments = new Dictionary<string, ListViewModel>();

            AddEntryCommand = new RelayCommand(
                () => {
                    CurrentArgumentList.AddNewItem(command: "", project: StartupProject, enabled: true);
                }, canExecute: _ =>
                {
                    return StartupProject != null;
                });

            RemoveEntriesCommand = new RelayCommand(
               () => {
                       CurrentArgumentList.DataCollection.RemoveRange(CurrentArgumentList.SelectedItems.Cast<CmdArgItem>());
               }, canExecute: _ =>
               {
                   return StartupProject != null && CurrentArgumentList.SelectedItems != null && CurrentArgumentList.SelectedItems.Count != 0;
               });

            MoveEntriesUpCommand = new RelayCommand(
               () => {
                       CurrentArgumentList.MoveEntriesUp(CurrentArgumentList.SelectedItems.Cast<CmdArgItem>());
               }, canExecute: _ =>
               {
                   return this.StartupProject != null && CurrentArgumentList.SelectedItems != null && CurrentArgumentList.SelectedItems.Count != 0;
               });

            MoveEntriesDownCommand = new RelayCommand(
               () => {
                       CurrentArgumentList.MoveEntriesDown(CurrentArgumentList.SelectedItems.Cast<CmdArgItem>());
               }, canExecute: _ =>
               {
                   return this.StartupProject != null && CurrentArgumentList.SelectedItems != null && CurrentArgumentList.SelectedItems.Count != 0;
               });

            ToogleItemsEnabledCommand = new RelayCommand(
                () => {
                    CurrentArgumentList.ToogleEnabledForItems(CurrentArgumentList.SelectedItems.Cast<CmdArgItem>());
                }, canExecute: _ =>
                {
                    return this.StartupProject != null;
                });
        }

        public IEnumerable<CmdArgItem> ActiveItemsForCurrentProject()
        {
            foreach (CmdArgItem item in CurrentArgumentList.DataCollection)
            {
                if (item.Enabled && item.Project == StartupProject)
                {
                    yield return item;
                }
            }
        }

        public void PopulateFromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            var entries = JsonConvert.DeserializeObject<Dictionary<string, ListViewModel>>(jsonStr);

            if (entries != null)
                solutionArguments = entries;
        }

        public void StoreToStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            string jsonStr = JsonConvert.SerializeObject(this.solutionArguments);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
            sw.Flush();
        }

        public void UpdateStartupProject(string projectName)
        {
            if (StartupProject == projectName) return;
            if (projectName == null)
            {
                UnsubscribeToChangeEvents();

                this.StartupProject = null;
                this.CurrentArgumentList = null;
            }
            else
            {
                UnsubscribeToChangeEvents();

                this.StartupProject = projectName;

                ListViewModel listVM = null;
                if (!solutionArguments.TryGetValue(projectName, out listVM))
                {
                    listVM = new ListViewModel();
                    solutionArguments.Add(projectName, listVM);
                }

                this.CurrentArgumentList = listVM;

                SubscribeToChangeEvents();
            }
        }

        private void SubscribeToChangeEvents()
        {
            if (CurrentArgumentList != null)
            {
                CurrentArgumentList.DataCollection.ItemPropertyChanged += OnArgumentListItemChanged;
                CurrentArgumentList.DataCollection.CollectionChanged += OnArgumentListChanged;
                CurrentArgumentList.SelectedItemsChanged += OnSelectedItemsChanged;
            }
        }


        private void UnsubscribeToChangeEvents()
        {
            if (CurrentArgumentList != null)
            {
                CurrentArgumentList.DataCollection.ItemPropertyChanged -= OnArgumentListItemChanged;
                CurrentArgumentList.DataCollection.CollectionChanged -= OnArgumentListChanged;
                CurrentArgumentList.SelectedItemsChanged -= OnSelectedItemsChanged;
            }
        }

        private void OnArgumentListItemChanged(object sender, EventArgs args)
        {
            OnCommandLineChanged();
        }

        private void OnArgumentListChanged(object sender, EventArgs args)
        {
            OnCommandLineChanged();
        }

        protected virtual void OnCommandLineChanged()
        {
            CommandLineChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSelectedItemsChanged(object obj, IList e)
        {
            SelectedItemsChanged?.Invoke(this, e);
        }
    }
}
