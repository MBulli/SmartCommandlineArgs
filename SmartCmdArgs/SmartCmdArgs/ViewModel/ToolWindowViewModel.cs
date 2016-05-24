using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SmartCmdArgs.Helper;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.ViewModel
{
    public class ToolWindowViewModel : PropertyChangedBase
    {
        private Dictionary<string, ListViewModel> solutionArguments; 
        public Dictionary<string, ListViewModel> SolutionArguments
        {
            get { return solutionArguments; }
        }

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

        private bool _isInEditMode;
        public bool IsInEditMode
        {
            get { return _isInEditMode; }
            set { _isInEditMode = value; OnNotifyPropertyChanged(); }
        }

        public RelayCommand AddEntryCommand { get; }

        public RelayCommand RemoveEntriesCommand { get; }

        public RelayCommand MoveEntriesUpCommand { get; }

        public RelayCommand MoveEntriesDownCommand { get; }

        public RelayCommand<CmdArgItem> ToggleItemEnabledCommand { get; }
        
        public RelayCommand CopySelectedItemsCommand { get; }
        
        public RelayCommand PasteItemsCommand { get; }
        
        public RelayCommand CutItemsCommand { get; }

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
                         RemoveSelectedItems();
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

            CopySelectedItemsCommand = new RelayCommand(CopySelectedItemsToClipboard, canExecute: _ => CurrentArgumentList.SelectedItems.Count != 0);

            PasteItemsCommand = new RelayCommand(PasteItemsFromClipboard, canExecute: _ => StartupProject != null);

            CutItemsCommand = new RelayCommand(CutItemsToClipboard, canExecute: _ => CurrentArgumentList.SelectedItems.Count != 0);
        }

        /// <summary>
        /// Resets the whole state of the tool window view model
        /// </summary>
        public void Reset()
        {
            UpdateStartupProject(null);

            solutionArguments.Clear();
        }

        private void CopySelectedItemsToClipboard()
        {
            var dataObject = new DataObject();

            var selectedItemsText = string.Join(
                                        Environment.NewLine,
                                        from x in CurrentArgumentList.SelectedItems.Cast<CmdArgItem>() select x.Command);
            dataObject.SetText(selectedItemsText);

            var selectedItemsJson = JsonConvert.SerializeObject(
                from x in CurrentArgumentList.SelectedItems.Cast<CmdArgItem>()
                select new CmdArgClipboardItem { Enabled = x.Enabled, Command = x.Command });
            dataObject.SetData(CmdArgsPackage.ClipboardCmdItemFormat, selectedItemsJson);

            Clipboard.SetDataObject(dataObject);
        }

        private void PasteItemsFromClipboard()
        {
            var pastedItemsJson = Clipboard.GetDataObject()?.GetData(CmdArgsPackage.ClipboardCmdItemFormat) as string;

            if (pastedItemsJson != null)
            {
                var pastedItems = JsonConvert.DeserializeObject<CmdArgClipboardItem[]>(pastedItemsJson);
                foreach (var item in pastedItems)
                {
                    CurrentArgumentList.AddNewItem(item.Command, StartupProject, item.Enabled);
                }
            }
            else if (Clipboard.ContainsText())
            {
                var pastedItems = Clipboard.GetText().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in pastedItems)
                {
                    CurrentArgumentList.AddNewItem(s, StartupProject);
                }
            }
        }

        private void CutItemsToClipboard()
        {
            CopySelectedItemsToClipboard();
            RemoveSelectedItems();
        }

        private void RemoveSelectedItems()
        {
            // TODO: select a row after delete
            //var selectedIndex = CurrentArgumentList.DataCollection.IndexOf((CmdArgItem) CurrentArgumentList.SelectedItems[0]);

            CurrentArgumentList.DataCollection.RemoveRange(CurrentArgumentList.SelectedItems.Cast<CmdArgItem>());
            
            //if (CurrentArgumentList.DataCollection.Count > 0)
            //{
            //    var newSelectionIndex = Math.Min(selectedIndex, CurrentArgumentList.DataCollection.Count - 1);
            //    CurrentArgumentList.SelectedItems.Add(CurrentArgumentList.DataCollection[newSelectionIndex]);
            //}
        }

        public IEnumerable<CmdArgItem> ActiveItemsForCurrentProject()
        {
            if (CurrentArgumentList == null)
            {
                yield break;
            }
            else
            {
                foreach (CmdArgItem item in CurrentArgumentList.DataCollection)
                {
                    if (item.Enabled && item.Project == StartupProject)
                    {
                        yield return item;
                    }
                }
            }
        }

        public void PopulateFromSolutionData(Logic.ToolWindowStateSolutionData data)
        {
            if (data == null)
                return;

            foreach (var projectCommandsPair in data)
            {
                var curListVM = GetListViewModel(projectCommandsPair.Key);
                foreach (var item in projectCommandsPair.Value.DataCollection)
                {
                    // TODO check dup key
                    curListVM.DataCollection.Add(new CmdArgItem() {
                        Id = item.Id,
                        Command = item.Command,
                        Enabled = item.Enabled,
                        Project = projectCommandsPair.Key
                    });
                }
            }
        }

        public void PopulateFromDictinary(Dictionary<string, IList<string>> dict)
        {
            if (dict == null)
                return;

            foreach (var projectCommandsPair in dict)
            {
                var curListVM = GetListViewModel(projectCommandsPair.Key);
                foreach (var command in projectCommandsPair.Value)
                {
                    curListVM.AddNewItem(command, projectCommandsPair.Key, false);
                }
            }
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

        public void CancelEdit()
        {
            System.Windows.Controls.DataGrid.CancelEditCommand.Execute(null, null);
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
