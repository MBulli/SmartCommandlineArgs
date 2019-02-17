using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.ViewModel
{
    public class ToolWindowViewModel : PropertyChangedBase
    {
        private static readonly string DefaultFontFamily = null;
        private static readonly string MonospaceFontFamily = "Consolas";

        public TreeViewModel TreeViewModel { get; }

        private string itemsFontFamily;
        public string ItemsFontFamily
        {
            get => itemsFontFamily;
            private set { SetAndNotify(value, ref itemsFontFamily); }
        }

        private bool useMonospaceFont;
        public bool UseMonospaceFont
        {
            get => useMonospaceFont;
            set
            {
                if (value != useMonospaceFont)
                {
                    if (value)
                        ItemsFontFamily = MonospaceFontFamily;
                    else
                        ItemsFontFamily = DefaultFontFamily;

                    SetAndNotify(value, ref useMonospaceFont);
                }
            }
        }

        public RelayCommand AddEntryCommand { get; }

        public RelayCommand AddGroupCommand { get; }

        public RelayCommand RemoveEntriesCommand { get; }

        public RelayCommand MoveEntriesUpCommand { get; }

        public RelayCommand MoveEntriesDownCommand { get; }

        public RelayCommand CopyCommandlineCommand { get; }

        public RelayCommand ShowAllProjectsCommand { get; }

        public RelayCommand ToggleSelectedCommand { get; }
        
        public RelayCommand CopySelectedItemsCommand { get; }
        
        public RelayCommand PasteItemsCommand { get; }
        
        public RelayCommand CutItemsCommand { get; }

        public RelayCommand SplitArgumentCommand { get; }

        private static Regex SplitArgumentRegex = new Regex(@"(?:""(?:""""|\\""|[^""])*""?|[^\s""]+)+", RegexOptions.Compiled);

        public ToolWindowViewModel()
        {
            TreeViewModel = new TreeViewModel();

            AddEntryCommand = new RelayCommand(
                () => {
                    var newArg = new CmdArgument(arg: "", isChecked: true);
                    TreeViewModel.AddItemAtFocusedItem(newArg);
                    if (TreeViewModel.SelectItemCommand.CanExecute(newArg))
                        TreeViewModel.SelectItemCommand.Execute(newArg);
                }, canExecute: _ => HasStartupProject());

            AddGroupCommand = new RelayCommand(
                () => {
                    var newGrp = new CmdGroup(name: "");
                    TreeViewModel.AddItemAtFocusedItem(newGrp);
                    if (TreeViewModel.SelectItemCommand.CanExecute(newGrp))
                        TreeViewModel.SelectItemCommand.Execute(newGrp);
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
                    
                    // copy=false see #58
                    Clipboard.SetDataObject(prjCmdArgs, copy: false);                   
                }, canExecute: _ => HasStartupProject());

            ShowAllProjectsCommand = new RelayCommand(
                () => {
                    TreeViewModel.ShowAllProjects = !TreeViewModel.ShowAllProjects;
                });

            ToggleSelectedCommand = new RelayCommand(
                () => {
                    TreeViewModel.ToggleSelected();
                }, canExecute: _ => HasStartupProject());

            CopySelectedItemsCommand = new RelayCommand(() => CopySelectedItemsToClipboard(includeProjects: true), canExecute: _ => HasSelectedItems());

            PasteItemsCommand = new RelayCommand(PasteItemsFromClipboard, canExecute: _ => HasStartupProject());

            CutItemsCommand = new RelayCommand(CutItemsToClipboard, canExecute: _ => HasSelectedItems());

            SplitArgumentCommand = new RelayCommand(() =>
            {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem == null || !(selectedItem is CmdArgument)) return;
                
                var newItems = SplitArgumentRegex.Matches(selectedItem.Value)
                               .Cast<Match>()
                               .Select((m) => new CmdArgument(arg: m.Value, isChecked: selectedItem.IsChecked ?? false))
                               .ToList();
                
                TreeViewModel.AddItemsAt(selectedItem, newItems);
                RemoveItems(new[] { selectedItem });
                TreeViewModel.SelectItems(newItems);
            }, canExecute: _ => HasSingleSelectedItem() && (TreeViewModel.SelectedItems.FirstOrDefault() is CmdArgument));
        }


        /// <summary>
        /// Resets the whole state of the tool window view model
        /// </summary>
        public void Reset()
        {
            TreeViewModel.Projects.Clear();
        }

        private void CopySelectedItemsToClipboard(bool includeProjects)
        {
            var selectedItems = TreeViewModel.Projects.Values.SelectMany(prj => prj.GetEnumerable(includeSelf: includeProjects)).Where(item => item.IsSelected).ToList();
            var set = new HashSet<CmdContainer>(selectedItems.OfType<CmdContainer>());
            var itemsToCopy = selectedItems.Where(x => !set.Contains(x.Parent));

            if (includeProjects)
                itemsToCopy = itemsToCopy.SelectMany(item => item is CmdProject prj ? prj.Items : Enumerable.Repeat(item, 1));

            var itemListToCopy = itemsToCopy.ToList();
            if (itemListToCopy.Count > 0)
                Clipboard.SetDataObject(DataObjectGenerator.Genrate(itemListToCopy, includeObject: false));
        }

        private void PasteItemsFromClipboard()
        {
            var pastedItems = DataObjectGenerator.Extract(Clipboard.GetDataObject(), includeObject: false)?.ToList();
            if (pastedItems != null && pastedItems.Count > 0)
            {
                TreeViewModel.AddItemsAtFocusedItem(pastedItems);
                TreeViewModel.SelectItems(pastedItems);
            }
        }

        private void CutItemsToClipboard()
        {
            CopySelectedItemsToClipboard(includeProjects: false);
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
        /// <returns>True if axactly one line is selected</returns>
        private bool HasSingleSelectedItem()
        {
            return TreeViewModel.SelectedItems.Count == 1;
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
            RemoveItems(TreeViewModel.SelectedItems, true);
        }

        private void RemoveItems(IEnumerable<CmdBase> items, bool doSelection = false)
        {
            var itemsSet = new HashSet<CmdBase>(items);

            int indexToSelect = 0;
            if (doSelection)
            {
                indexToSelect = TreeViewModel.TreeItemsView.OfType<CmdBase>()
                    .SelectMany(item => item is CmdContainer con ? con.GetEnumerable(true, true, false) : Enumerable.Repeat(item, 1))
                    .TakeWhile(item => !itemsSet.Contains(item)).Count();
            }

            bool removedAnItem = false;
            foreach (var item in itemsSet)
            {
                if (item.Parent != null)
                {
                    item.Parent.Items.Remove(item);
                    TreeViewModel.SelectedItems.Remove(item);
                    removedAnItem = true;
                }
            }
            if (!removedAnItem)
                return;

            if (doSelection)
            {
                indexToSelect = TreeViewModel.TreeItemsView.OfType<CmdBase>()
                    .SelectMany(item => item is CmdContainer con ? con.GetEnumerable(true, true, false) : Enumerable.Repeat(item, 1))
                    .Take(indexToSelect + 1).Count() - 1;
                if (TreeViewModel.SelectIndexCommand.CanExecute(indexToSelect))
                    TreeViewModel.SelectIndexCommand.Execute(indexToSelect);
            }
        }

        public void PopulateFromProjectData(IVsHierarchy project, ToolWindowStateProjectData data)
        {
            var guid = project.GetGuid();
            var cmdPrj = new CmdProject(guid, project.GetKind(), project.GetDisplayName());
            cmdPrj.Items.AddRange(ListEntriesToCmdObjects(data.Items));

            // Assign TreeViewModel after AddRange to not get a lot of ParentChanged events
            cmdPrj.ParentTreeViewModel = TreeViewModel; 

            TreeViewModel.Projects[guid] = cmdPrj;

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

        public void RenameProject(IVsHierarchy project)
        {
            if (TreeViewModel.Projects.TryGetValue(project.GetGuid(), out CmdProject cmdProject))
            {
                cmdProject.Value = project.GetDisplayName();
            }
        }
    }
}
