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
using JB.Collections.Reactive;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.ViewModel
{
    public class ToolWindowViewModel : PropertyChangedBase
    {
        public static readonly string DefaultFontFamily = null;
        public static readonly string MonospaceFontFamily = "Consolas";
        
        public TreeViewModel TreeViewModel { get; }

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

        public RelayCommand AddGroupCommand { get; }

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
            TreeViewModel = new TreeViewModel();

            AddEntryCommand = new RelayCommand(
                () => {
                    TreeViewModel.FocusedProject?.AddNewArgument(command: "", enabled: true);
                }, canExecute: _ => HasStartupProject());

            AddGroupCommand = new RelayCommand(
                () => {
                    TreeViewModel.FocusedProject?.AddNewGroup(command: "");
                }, canExecute: _ => HasStartupProject());

            RemoveEntriesCommand = new RelayCommand(
               () => {
                         RemoveSelectedItems();
               }, canExecute: _ => HasStartupProjectAndSelectedItems());

            MoveEntriesUpCommand = new RelayCommand(
               () => {
                       //CurrentArgumentList.MoveEntriesUp(CurrentArgumentList.SelectedItems.Cast<CmdArgItem>());
               }, canExecute: _ => HasStartupProjectAndSelectedItems());

            MoveEntriesDownCommand = new RelayCommand(
               () => {
                       //CurrentArgumentList.MoveEntriesDown(CurrentArgumentList.SelectedItems.Cast<CmdArgItem>());
               }, canExecute: _ => HasStartupProjectAndSelectedItems());

            CopyCommandlineCommand = new RelayCommand(
               () => {
                   IEnumerable<string> enabledEntries;
                   enabledEntries = TreeViewModel.FocusedProject.CheckedArguments.Select(e => e.Value);
                   string prjCmdArgs = string.Join(" ", enabledEntries);
                   Clipboard.SetText(prjCmdArgs);
               }, canExecute: _ => HasStartupProject());

            ToggleItemEnabledCommand = new RelayCommand<CmdArgItem>(
                (item) => {
                    //CurrentArgumentList.ToogleEnabledForItem(item, Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt));
                }, canExecute: _ => HasStartupProject());

            CopySelectedItemsCommand = new RelayCommand(CopySelectedItemsToClipboard, canExecute: _ => HasSelectedItems());

            PasteItemsCommand = new RelayCommand(PasteItemsFromClipboard, canExecute: _ => HasStartupProject());

            CutItemsCommand = new RelayCommand(CutItemsToClipboard, canExecute: _ => HasSelectedItems());

            TreeViewModel.Projects.DictionaryItemChanges.Subscribe(OnArgumentListItemChanged);
            TreeViewModel.Projects.CollectionChanged += OnArgumentListChanged;
            TreeViewModel.SelectedItemsChanged += OnSelectedItemsChanged;
        }

        private void OnNext(IObservableDictionaryChange<string, CmdProject> observableDictionaryChange)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Resets the whole state of the tool window view model
        /// </summary>
        public void Reset()
        {
            TreeViewModel.Projects.Clear();
        }

        private void CopySelectedItemsToClipboard()
        {
            var dataObject = new DataObject();

            var selectedItemsText = string.Join(
                                        Environment.NewLine,
                                        from x in TreeViewModel.Projects.Values.SelectMany(p => p.SelectedArguments) select x.Value);
            dataObject.SetText(selectedItemsText);

            var selectedItemsJson = JsonConvert.SerializeObject(
                from x in TreeViewModel.Projects.Values.SelectMany(p => p.SelectedArguments)
                select new CmdArgClipboardItem { Enabled = x.IsChecked == true, Command = x.Value });
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
                    TreeViewModel.FocusedProject?.AddNewArgument(item.Command, item.Enabled);
                }
            }
            else if (Clipboard.ContainsText())
            {
                var pastedItems = Clipboard.GetText().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in pastedItems)
                {
                    TreeViewModel.FocusedProject?.AddNewArgument(s);
                }
            }
        }

        private void CutItemsToClipboard()
        {
            CopySelectedItemsToClipboard();
            RemoveSelectedItems();
        }


        /// <summary>
        /// Helper method for CanExecute condition of the commands
        /// </summary>
        /// <returns>True if a valid startup project is set</returns>
        private bool HasStartupProject()
        {
            return TreeViewModel.StartupProjects.Any();
        }

        /// <summary>
        /// Helper method for CanExecute condition of the commands
        /// </summary>
        /// <returns>True if any line is selected</returns>
        private bool HasSelectedItems()
        {
            return TreeViewModel.Projects.Values.SelectMany(p => p.SelectedItems).Any();
        }

        /// <summary>
        /// Helper method for CanExecute condition of the commands
        /// </summary>
        /// <returns>True if a valid startup project is set and any line is selected</returns>
        private bool HasStartupProjectAndSelectedItems()
        {
            return HasStartupProject() && HasSelectedItems();
        }

        private void RemoveSelectedItems()
        {
            // This will eventually bubble down to the DataGridView
            foreach (var item in TreeViewModel.Projects.Values.SelectMany(p => p.SelectedItems).ToList())
            {
                item.Parent.Items.Remove(item);
            }
        }

        public void PopulateFromProjectData(string projectName, ToolWindowStateProjectData data)
        {
            var cmdProject = GetCmdProject(projectName);
            cmdProject.Items.Clear();
            cmdProject.Items.AddRange(ListEntriesToCmdObjects(data.DataCollection));

            IEnumerable<CmdBase> ListEntriesToCmdObjects(List<ToolWindowStateProjectData.ListEntryData> list)
            {
                foreach (var item in list)
                {
                    if (item.Items == null)
                        yield return new CmdArgument(item.Id, item.Command, item.Enabled);
                    else
                        yield return new CmdGroup(item.Command, ListEntriesToCmdObjects(item.Items));
                }
            }
        }

        public CmdProject GetCmdProject(string projectName)
        {
            CmdProject cmdProject;
            if (!TreeViewModel.Projects.TryGetValue(projectName, out cmdProject))
            {
                cmdProject = new CmdProject(projectName);
                TreeViewModel.Projects.Add(projectName, cmdProject);
            }
            return cmdProject;
        }

        public void RenameProject(string oldName, string newName)
        {
            if (TreeViewModel.Projects.TryGetValue(oldName, out CmdProject cmdProject))
            {
                TreeViewModel.Projects.Remove(oldName);
                cmdProject.Value = newName;
                TreeViewModel.Projects.Add(newName, cmdProject);
            }
        }

        public void CancelEdit()
        {
            System.Windows.Controls.DataGrid.CancelEditCommand.Execute(null, null);
        }

        private void OnArgumentListItemChanged(IObservableDictionaryChange<string, CmdProject> change)
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
