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
        private static readonly Regex SplitArgumentRegex = new Regex(@"(?:""(?:""""|\\""|[^""])*""?|[^\s""]+)+", RegexOptions.Compiled);

        public TreeViewModel TreeViewModel { get; }

        public CmdArgsPackage CmdArgsPackage { get; }

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

        public RelayCommand NewGroupFromArgumentsCommand { get; }

        public RelayCommand SetAsStartupProjectCommand { get; set; }

        public ToolWindowViewModel(CmdArgsPackage package)
        {
            CmdArgsPackage = package;

            TreeViewModel = new TreeViewModel();

            AddEntryCommand = new RelayCommand(
                () => {
                    var newArg = new CmdArgument(arg: "", isChecked: true);
                    TreeViewModel.AddItemAtFocusedItem(newArg);
                    TreeViewModel.SelectItemCommand.SafeExecute(newArg);
                }, canExecute: _ => HasStartupProject());

            AddGroupCommand = new RelayCommand(
                () => {
                    var newGrp = new CmdGroup(name: "");
                    TreeViewModel.AddItemAtFocusedItem(newGrp);
                    TreeViewModel.SelectItemCommand.SafeExecute(newGrp);
                }, canExecute: _ => HasStartupProject());

            RemoveEntriesCommand = new RelayCommand(
                () => {
                    RemoveSelectedItems();
                }, canExecute: _ => HasStartupProjectAndSelectedItems() && !HasSingleSelectedItemOfType<CmdProject>());

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
                    var focusedProject = TreeViewModel.FocusedProject;
                    if (focusedProject == null)
                        return;

                    var prjCmdArgs = CmdArgsPackage.CreateCommandLineArgsForProject(focusedProject.Id);
                    if (prjCmdArgs == null)
                        return;
                    
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

            CutItemsCommand = new RelayCommand(CutItemsToClipboard, canExecute: _ => HasSelectedItems() && !HasSingleSelectedItemOfType<CmdProject>());

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
            }, canExecute: _ => HasSingleSelectedItemOfType<CmdArgument>());

            NewGroupFromArgumentsCommand = new RelayCommand(() =>
            {
                var itemsToGroup = GetSelectedRootItems(true).ToList();

                if (itemsToGroup.Count == 0)
                    return;
                
                CmdBase firstElement = itemsToGroup.First();
                CmdContainer parent = firstElement.Parent;

                // add new group
                var newGrp = new CmdGroup(name: "");
                var insertIndex = parent.TakeWhile((item) => item != firstElement).Count();
                parent.Items.Insert(insertIndex, newGrp);
                
                // move items to new group
                parent.Items.RemoveRange(itemsToGroup);
                newGrp.Items.AddRange(itemsToGroup);
                
                // set selection to new group
                TreeViewModel.SelectItemCommand.SafeExecute(newGrp);

            }, _ => HasSelectedItems() && HaveSameParent(GetSelectedRootItems(true)));
            
            SetAsStartupProjectCommand = new RelayCommand(() => {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdProject proj)
                {
                    CmdArgsPackage.SetAsStartupProject(proj.Id);
                }
            }, _ => HasSingleSelectedItemOfType<CmdProject>());
        }


        /// <summary>
        /// Resets the whole state of the tool window view model
        /// </summary>
        public void Reset()
        {
            TreeViewModel.Projects.Clear();
        }

        private bool HaveSameParent(IEnumerable<CmdBase> itmes)
        {
            CmdContainer parent = null;
            foreach (var item in itmes)
            {
                if (parent == null)
                    parent = item.Parent;
                else if (parent != item.Parent)
                    return false;
            }
            return parent != null;
        }

        private IEnumerable<CmdBase> GetSelectedRootItems(bool includeProjects)
        {
            var selectedItems = TreeViewModel.Projects.Values.SelectMany(prj => prj.GetEnumerable(includeSelf: includeProjects)).Where(item => item.IsSelected).ToList();
            var set = new HashSet<CmdContainer>(selectedItems.OfType<CmdContainer>());
            var result = selectedItems.Where(x => !set.Contains(x.Parent));

            if (includeProjects)
                result = result.SelectMany(item => item is CmdProject prj ? prj.Items : Enumerable.Repeat(item, 1));

            return result;
        }

        private void CopySelectedItemsToClipboard(bool includeProjects)
        {
            var itemListToCopy = GetSelectedRootItems(includeProjects).ToList();
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
        /// <returns>True if an line of the given type is selected</returns>
        private bool HasSelectedItemOfType<T>()
        {
            return TreeViewModel.SelectedItems.OfType<T>().Any();
        }
        
        /// <summary>
        /// Helper method for CanExecute condition of the commands
        /// </summary>
        /// <returns>True if axactly one line is selected and it is of the given type.</returns>
        private bool HasSingleSelectedItemOfType<T>()
        {
            return HasSingleSelectedItem() && (TreeViewModel.SelectedItems.First() is T);
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
                TreeViewModel.SelectIndexCommand.SafeExecute(indexToSelect);
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
