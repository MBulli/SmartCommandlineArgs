using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EnvDTE;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.ViewModel
{
    public class ToolWindowViewModel : PropertyChangedBase
    {
        public static readonly string DefaultFontFamily = null;
        public static readonly string MonospaceFontFamily = "Consolas";

        private Dictionary<string, ListViewModel> solutionArguments; 
        public Dictionary<string, ListViewModel> SolutionArguments => solutionArguments;

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

        private string _usedFontFamily;
        public string UsedFontFamily
        {
            get => _usedFontFamily;
            private set { _usedFontFamily = value; OnNotifyPropertyChanged(); }
        }

        private bool _useMonospaceFont;
        public bool UseMonospaceFont
        {
            get => _useMonospaceFont;
            set
            {
                if (value == _useMonospaceFont)
                    return;

                if (value)
                    UsedFontFamily = MonospaceFontFamily;
                else
                    UsedFontFamily = DefaultFontFamily;
                _useMonospaceFont = value;
            }
        }

        public RelayCommand AddEntryCommand { get; }

        public RelayCommand RemoveEntriesCommand { get; }

        public RelayCommand MoveEntriesUpCommand { get; }

        public RelayCommand MoveEntriesDownCommand { get; }

        public RelayCommand CopyCommandlineCommand { get; }

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
                    CurrentArgumentList.AddNewItem(command: "", enabled: true);
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

            CopyCommandlineCommand = new RelayCommand(
               () => {
                   IEnumerable<string> enabledEntries;
                   enabledEntries = this.EnabledItemsForCurrentProject().Select(e => e.Command);
                   string prjCmdArgs = string.Join(" ", enabledEntries);
                   Clipboard.SetText(prjCmdArgs);
               }, canExecute: _ =>
               {
                   return this.StartupProject != null; // && CurrentArgumentList.SelectedItems != null && CurrentArgumentList.SelectedItems.Count != 0;
               });

            ToggleItemEnabledCommand = new RelayCommand<CmdArgItem>(
                (item) => {
                    CurrentArgumentList.ToogleEnabledForItem(item, Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt));
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
                    CurrentArgumentList.AddNewItem(item.Command, item.Enabled);
                }
            }
            else if (Clipboard.ContainsText())
            {
                var pastedItems = Clipboard.GetText().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in pastedItems)
                {
                    CurrentArgumentList.AddNewItem(s);
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
            // This will eventually bubble down to the DataGridView
            System.Windows.Input.ApplicationCommands.Delete.Execute(parameter: null, target: null);
        }

        public IEnumerable<CmdArgItem> EnabledItemsForCurrentProject()
        {
            return CurrentArgumentList?.DataCollection.Where(item => item.Enabled) ?? new CmdArgItem[0];
        }

        public void PopulateFromProjectData(string projectName, ToolWindowStateProjectData data)
        {
            var curListVM = GetListViewModel(projectName);
            curListVM.DataCollection.Clear();
            curListVM.DataCollection.AddRange(
                data.DataCollection.Select(
                    // TODO check dup key
                    item => new CmdArgItem {
                        Id = item.Id,
                        Command = item.Command,
                        Enabled = item.Enabled
                    }));
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

        public bool UpdateStartupProject(string projectName)
        {
            if (StartupProject == projectName) return false;
            if (string.IsNullOrEmpty(projectName))
            {
                UnsubscribeToChangeEvents();

                this.StartupProject = null;
                this.CurrentArgumentList = null;

                Logger.Info("Reset Startup project");
            }
            else
            {
                UnsubscribeToChangeEvents();

                this.StartupProject = projectName;
                this.CurrentArgumentList = GetListViewModel(projectName);

                SubscribeToChangeEvents();

                Logger.Info($"Startup project changed to '{projectName}'.");
            }
            return true;
        }

        public void RenameProject(string oldName, string newName)
        {
            if (SolutionArguments.TryGetValue(oldName, out ListViewModel vm))
            {
                SolutionArguments.Remove(oldName);
                solutionArguments.Add(newName, vm);
            }
            if (StartupProject == oldName)
                StartupProject = newName;
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

        private void OnArgumentListItemChanged(object sender, CollectionItemPropertyChangedEventArgs<CmdArgItem> args)
        {
            OnCommandLineChanged();
        }

        private void OnArgumentListChanged(object sender, NotifyCollectionChangedEventArgs args)
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
