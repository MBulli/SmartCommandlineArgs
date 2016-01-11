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
        private bool populatedFromStream;
        private bool populatedFromDictionary;

        public bool Initialized => populatedFromDictionary || populatedFromStream;

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

        public RelayCommand<CmdArgItem> ToggleItemEnabledCommand { get; }

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

            ToggleItemEnabledCommand = new RelayCommand<CmdArgItem>(
                (item) => {
                    CurrentArgumentList.ToogleEnabledForItem(item);
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

        public void PopulateFromDictinary(Dictionary<string, IList<string>> dict)
        {
            foreach (var projectCommandsPair in dict)
            {
                var curListVM = GetListViewModel(projectCommandsPair.Key);
                foreach (var command in projectCommandsPair.Value)
                {
                    curListVM.AddNewItem(command, projectCommandsPair.Key, false);
                }
            }
            populatedFromDictionary = true;
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

            populatedFromStream = true;
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

        public ListViewModel GetListViewModel(string projectName)
        {
            ListViewModel listVM;
            if (!solutionArguments.TryGetValue(projectName, out listVM))
            {
                listVM = new ListViewModel();
                solutionArguments.Add(projectName, listVM);
            }
            return listVM;
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
                this.CurrentArgumentList = GetListViewModel(projectName);

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
