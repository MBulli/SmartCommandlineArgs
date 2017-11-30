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

        public RelayCommand<CmdBase> ToggleItemEnabledCommand { get; }
        
        public RelayCommand CopySelectedItemsCommand { get; }
        
        public RelayCommand PasteItemsCommand { get; }
        
        public RelayCommand CutItemsCommand { get; }

        public event EventHandler CommandLineChanged;

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
                    TreeViewModel.MoveSelectedEntries(moveDirection: -1);
                }, canExecute: _ => HasStartupProjectAndSelectedItems());

            MoveEntriesDownCommand = new RelayCommand(
                () => {
                    TreeViewModel.MoveSelectedEntries(moveDirection: 1);
                }, canExecute: _ => HasStartupProjectAndSelectedItems());

            CopyCommandlineCommand = new RelayCommand(
                () => {
                    IEnumerable<string> enabledEntries;
                    enabledEntries = TreeViewModel.FocusedProject.CheckedArguments.Select(e => e.Value);
                    string prjCmdArgs = string.Join(" ", enabledEntries);
                    Clipboard.SetText(prjCmdArgs);
                }, canExecute: _ => HasStartupProject());

            ToggleItemEnabledCommand = new RelayCommand<CmdBase>(
                (item) => {
                    //CurrentArgumentList.ToogleEnabledForItem(item, Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt));
                }, canExecute: _ => HasStartupProject());

            CopySelectedItemsCommand = new RelayCommand(CopySelectedItemsToClipboard, canExecute: _ => HasSelectedItems());

            PasteItemsCommand = new RelayCommand(PasteItemsFromClipboard, canExecute: _ => HasStartupProject());

            CutItemsCommand = new RelayCommand(CutItemsToClipboard, canExecute: _ => HasSelectedItems());

            TreeViewModel.Projects.ItemPropertyChanged += OnArgumentListItemChanged;
            TreeViewModel.Projects.CollectionChanged += OnArgumentListChanged;
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
            var selectedItems = TreeViewModel.Projects.Values.SelectMany(prj => prj.SelectedItems).ToList();
            var set = new HashSet<CmdBase>(selectedItems.OfType<CmdContainer>());
            var itemsToCopy = selectedItems.Where(x => !set.Contains(x.Parent)).ToList();

            Clipboard.SetDataObject(DataObjectGenerator.Genrate(itemsToCopy, includeObject: false));
        }

        private void PasteItemsFromClipboard()
        {
            var pastedItems = DataObjectGenerator.Extract(Clipboard.GetDataObject(), includeObject: false);
            TreeViewModel.FocusedProject.Items.AddRange(pastedItems);
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
            return TreeViewModel.SelectedItems.Count > 0;
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
            foreach (var item in TreeViewModel.SelectedItems.ToList())
            {
                item.Parent.Items.Remove(item);
            }
            TreeViewModel.SelectedItems.Clear();
        }

        public void PopulateFromProjectData(Project project, ToolWindowStateProjectData data)
        {
            TreeViewModel.Projects[project.UniqueName] = new CmdProject(data.Id, project.Name, ListEntriesToCmdObjects(data.Items));

            IEnumerable<CmdBase> ListEntriesToCmdObjects(List<ListEntryData> list)
            {
                foreach (var item in list)
                {
                    if (item.Items == null)
                        yield return new CmdArgument(item.Id, item.Command, item.Enabled);
                    else
                        yield return new CmdGroup(item.Id, item.Command, ListEntriesToCmdObjects(item.Items), item.Expanded);
                }
            }
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

        private void OnArgumentListItemChanged(object sender, CollectionItemPropertyChangedEventArgs<CmdProject> args)
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
    }
}
