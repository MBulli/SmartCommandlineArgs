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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using SmartCmdArgs.Services;
using SmartCmdArgs.View;

namespace SmartCmdArgs.ViewModel
{
    public class ToolWindowViewModel : PropertyChangedBase, IDisposable
    {
        private readonly IItemEvaluationService itemEvaluation;
        private readonly Lazy<ILifeCycleService> lifeCycleService;

        public TreeViewModel TreeViewModel { get; }

        public CmdArgsPackage CmdArgsPackage { get; }


        private bool useMonospaceFont;
        public bool UseMonospaceFont
        {
            get => useMonospaceFont;
            set => SetAndNotify(value, ref useMonospaceFont);
        }

        private bool _displayTagForCla;
        public bool DisplayTagForCla
        {
            get => _displayTagForCla;
            set => SetAndNotify(value, ref _displayTagForCla);
        }


        private bool _showDisabledScreen;
        public bool ShowDisabledScreen {
            get => _showDisabledScreen;
            set => SetAndNotify(value, ref _showDisabledScreen);
        }

        public RelayCommand<ArgumentType> AddEntryCommand { get; }

        public RelayCommand AddGroupCommand { get; }

        public RelayCommand RemoveEntriesCommand { get; }

        public RelayCommand MoveEntriesUpCommand { get; }

        public RelayCommand MoveEntriesDownCommand { get; }

        public RelayCommand CopyCommandlineCommand { get; }

        public RelayCommand<string> CopyEnvVarsForCommadlineCommand { get; }

        public RelayCommand ShowAllProjectsCommand { get; }

        public RelayCommand ShowSettingsCommand { get; }

        public RelayCommand OpenOptionsCommand { get; }

        public RelayCommand ToggleSelectedCommand { get; }
        
        public RelayCommand CopySelectedItemsCommand { get; }
        
        public RelayCommand PasteItemsCommand { get; }
        
        public RelayCommand CutItemsCommand { get; }

        public RelayCommand UndoCommand { get; }

        public RelayCommand RedoCommand { get; }

        public RelayCommand SplitArgumentCommand { get; }

        public RelayCommand RevealFileInExplorerCommand { get; }

        public RelayCommand OpenFileCommand { get; }

        public RelayCommand OpenFileInVSCommand { get; }

        public RelayCommand OpenDirectoryCommand { get; }

        public RelayCommand NewGroupFromArgumentsCommand { get; }

        public RelayCommand SetAsStartupProjectCommand { get; }

        public RelayCommand<string> SetProjectConfigCommand { get; }

        public RelayCommand<string> SetProjectPlatformCommand { get; }

        public RelayCommand<string> SetLaunchProfileCommand { get; }

        public RelayCommand ToggleExclusiveModeCommand { get; }

        public RelayCommand<string> SetDelimiterCommand { get; }

        public RelayCommand<ArgumentType> SetArgumentTypeCommand { get; }

        public RelayCommand ToggleDefaultCheckedCommand { get; }

        public RelayCommand ResetToDefaultCheckedCommand { get; }

        public RelayCommand EnableExtensionCommand { get; }

        public ToolWindowViewModel(
            IItemEvaluationService itemEvaluation,
            IItemAggregationService itemAggregation,
            SettingsViewModel settingsViewModel,
            ISettingsService settingsService,
            IVisualStudioHelperService vsHelper,
            Lazy<ILifeCycleService> lifeCycleService)
        {
            this.itemEvaluation = itemEvaluation;
            this.lifeCycleService = lifeCycleService;

            CmdArgsPackage = CmdArgsPackage.Instance;

            TreeViewModel = new TreeViewModel();

            ToolWindowHistory.Init(this, settingsViewModel);

            AddEntryCommand = new RelayCommand<ArgumentType>(
                argType => {
                    ToolWindowHistory.SaveState();
                    var newArg = new CmdArgument(argType, arg: "", isChecked: true);
                    TreeViewModel.AddItemAtFocusedItem(newArg);
                    TreeViewModel.SelectItemCommand.SafeExecute(newArg);
                }, canExecute: _ => ExtensionEnabled && HasStartupProject());

            AddGroupCommand = new RelayCommand(
                () => {
                    ToolWindowHistory.SaveState();
                    var newGrp = new CmdGroup(name: "");
                    TreeViewModel.AddItemAtFocusedItem(newGrp);
                    TreeViewModel.SelectItemCommand.SafeExecute(newGrp);
                }, canExecute: _ => ExtensionEnabled && HasStartupProject());

            RemoveEntriesCommand = new RelayCommand(
                () => {
                    RemoveSelectedItems();
                }, canExecute: _ => ExtensionEnabled && HasStartupProjectAndSelectedItems() && !HasSingleSelectedItemOfType<CmdProject>());

            MoveEntriesUpCommand = new RelayCommand(
                () => {
                    ToolWindowHistory.SaveState();
                    TreeViewModel.MoveSelectedEntries(moveDirection: -1);
                }, canExecute: _ => ExtensionEnabled && HasStartupProjectAndSelectedItems());

            MoveEntriesDownCommand = new RelayCommand(
                () => {
                    ToolWindowHistory.SaveState();
                    TreeViewModel.MoveSelectedEntries(moveDirection: 1);
                }, canExecute: _ => ExtensionEnabled && HasStartupProjectAndSelectedItems());

            CopyCommandlineCommand = new RelayCommand(
                () => {
                    var focusedProject = TreeViewModel.FocusedProject;
                    if (focusedProject == null)
                        return;

                    var prjCmdArgs = itemAggregation.CreateCommandLineArgsForProject(focusedProject.Id);
                    if (prjCmdArgs == null)
                        return;
                    
                    // copy=false see #58
                    Clipboard.SetDataObject(prjCmdArgs, copy: false);                   
                }, canExecute: _ => ExtensionEnabled && HasStartupProject());

            var cmdEscapeRegex = new Regex("([&|(=<>^])", RegexOptions.Compiled);
            CopyEnvVarsForCommadlineCommand = new RelayCommand<string>(
                commandLineType => {
                    var focusedProject = TreeViewModel.FocusedProject;
                    if (focusedProject == null)
                        return;

                    var prjEnvVars = itemAggregation.GetEnvVarsForProject(focusedProject.Id);
                    if (prjEnvVars == null)
                        return;

                    string envVarStr;
                    switch (commandLineType)
                    {
                        case "PS":
                            envVarStr = string.Join(" ", prjEnvVars.Select(x => $"$env:{x.Key} = '{x.Value.Replace("'", "''")}';"));
                            break;

                        case "CMD":
                            envVarStr = string.Join(" && ", prjEnvVars.Select(x => $"set {x.Key}={cmdEscapeRegex.Replace(x.Value, "^$1")}"));
                            break;

                        default:
                            envVarStr = "";
                            break;
                    }

                    // copy=false see #58
                    Clipboard.SetDataObject(envVarStr, copy: false);
                }, canExecute: _ => ExtensionEnabled && HasStartupProject());

            ShowAllProjectsCommand = new RelayCommand(
                () => {
                    ToolWindowHistory.SaveState();
                    TreeViewModel.ShowAllProjects = !TreeViewModel.ShowAllProjects;
                }, canExecute: _ => ExtensionEnabled && settingsService.Loaded);

            ShowSettingsCommand = new RelayCommand(
                () => {
                    var settingsClone = settingsViewModel.Clone();
                    if (new SettingsDialog(settingsClone).ShowModal() == true)
                    {
                        settingsViewModel.Assign(settingsClone);
                        settingsService.Save();
                    }
                }, canExecute: _ => settingsService.Loaded);

            OpenOptionsCommand = new RelayCommand(
                () => {
                    CmdArgsPackage.ShowOptionPage(typeof(CmdArgsOptionPage));
                });

            ToggleSelectedCommand = new RelayCommand(
                () => {
                    ToolWindowHistory.SaveStateAndPause();
                    TreeViewModel.ToggleSelected();
                    ToolWindowHistory.Resume();
                }, canExecute: _ => ExtensionEnabled && HasStartupProject());

            CopySelectedItemsCommand = new RelayCommand(() => CopySelectedItemsToClipboard(includeProjects: true), canExecute: _ => ExtensionEnabled && HasSelectedItems());

            PasteItemsCommand = new RelayCommand(PasteItemsFromClipboard, canExecute: _ => ExtensionEnabled && HasStartupProject());

            CutItemsCommand = new RelayCommand(CutItemsToClipboard, canExecute: _ => ExtensionEnabled && HasSelectedItems() && !HasSingleSelectedItemOfType<CmdProject>());

            UndoCommand = new RelayCommand(ToolWindowHistory.RestoreLastState, _ => ExtensionEnabled && !TreeViewModel.IsInEditMode);

            RedoCommand = new RelayCommand(ToolWindowHistory.RestorePrevState, _ => ExtensionEnabled && !TreeViewModel.IsInEditMode);

            SplitArgumentCommand = new RelayCommand(() =>
            {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdArgument argument)
                {
                    ToolWindowHistory.SaveState();

                    var newItems = itemEvaluation.SplitArgument(argument.Value)
                                   .Select((s) => new CmdArgument(argument.ArgumentType, s, argument.IsChecked, argument.DefaultChecked))
                                   .ToList();

                    TreeViewModel.AddItemsAt(selectedItem, newItems);
                    RemoveItems(new[] { selectedItem });
                    TreeViewModel.SelectItems(newItems);
                }
            }, canExecute: _ => ExtensionEnabled && HasSingleSelectedArgumentOfType(ArgumentType.CmdArg));

            RevealFileInExplorerCommand = new RelayCommand(() =>
            {
                var fileName = ExtractFileNameFromSelectedArgument();
                if (fileName != null)
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fileName}\"");
            }, canExecute: _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdArgument>() && ExtractFileNameFromSelectedArgument() != null);

            OpenFileCommand = new RelayCommand(() =>
            {
                var fileName = ExtractFileNameFromSelectedArgument();
                if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
                {
                    System.Diagnostics.Process.Start(fileName);
                }
            }, canExecute: _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdArgument>() && ExtractFileNameFromSelectedArgument() != null);

            OpenFileInVSCommand = new RelayCommand(() =>
            {
                var fileName = ExtractFileNameFromSelectedArgument();
                if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
                {
                    var task = vsHelper.OpenFileInVisualStudioAsync(fileName);
                }
            }, canExecute: _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdArgument>() && ExtractFileNameFromSelectedArgument() != null);

            OpenDirectoryCommand = new RelayCommand(() =>
            {
                var directoryName = ExtractDirectoryNameFromSelectedArgument();
                if (directoryName != null)
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{directoryName}\"");
            }, canExecute: _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdArgument>() && ExtractDirectoryNameFromSelectedArgument() != null);

            NewGroupFromArgumentsCommand = new RelayCommand(() =>
            {
                var itemsToGroup = GetSelectedRootItems(true).ToList();

                if (itemsToGroup.Count == 0)
                    return;

                ToolWindowHistory.SaveState();

                CmdBase firstElement = itemsToGroup.First();
                CmdContainer parent = firstElement.Parent;

                // add new group
                var newGrp = new CmdGroup(name: "");
                var insertIndex = parent.Items.TakeWhile((item) => item != firstElement).Count();
                parent.Insert(insertIndex, newGrp);
                
                // move items to new group
                parent.Items.RemoveRange(itemsToGroup);
                itemsToGroup.ForEach(item => item.IsSelected = false);
                newGrp.AddRange(itemsToGroup);
                
                // set selection to new group
                TreeViewModel.SelectItemCommand.SafeExecute(newGrp);

            }, _ => ExtensionEnabled && HasSelectedItems() && HaveSameParent(GetSelectedRootItems(true)));
            
            SetAsStartupProjectCommand = new RelayCommand(() => {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdProject proj)
                {
                    vsHelper.SetAsStartupProject(proj.Id);
                }
            }, _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdProject>());

            SetProjectConfigCommand = new RelayCommand<string>(configName =>
            {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdGroup grp)
                {
                    ToolWindowHistory.SaveState();
                    grp.ProjectConfig = configName;
                }
            }, _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdGroup>());

            SetProjectPlatformCommand = new RelayCommand<string>(platformName =>
            {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdGroup grp)
                {
                    ToolWindowHistory.SaveState();
                    grp.ProjectPlatform = platformName;
                }
            }, _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdGroup>());

            SetLaunchProfileCommand = new RelayCommand<string>(profileName =>
            {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdGroup grp)
                {
                    ToolWindowHistory.SaveState();
                    grp.LaunchProfile = profileName;
                }
            }, _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdGroup>());

            ToggleExclusiveModeCommand = new RelayCommand(() =>
            {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdContainer con)
                {
                    ToolWindowHistory.SaveState();
                    con.ExclusiveMode = !con.ExclusiveMode;
                }
            }, _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdContainer>());

            SetDelimiterCommand = new RelayCommand<string>(delimiter =>
            {
                var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
                if (selectedItem is CmdContainer con && con.Delimiter != delimiter)
                {
                    ToolWindowHistory.SaveState();
                    con.Delimiter = delimiter;
                }
            }, _ => ExtensionEnabled && HasSingleSelectedItemOfType<CmdContainer>());

            SetArgumentTypeCommand = new RelayCommand<ArgumentType>(type =>
            {
                var items = TreeViewModel.SelectedItems.OfType<CmdArgument>().ToList();
                items.ForEach(x => x.ArgumentType = type);
            }, _ => ExtensionEnabled && HasSelectedItemOfType<CmdArgument>());

            ToggleDefaultCheckedCommand = new RelayCommand(() =>
            {
                var items = TreeViewModel.SelectedItems.OfType<CmdArgument>().ToList();
                var hasTrue = items.Any(x => x.DefaultChecked);
                items.ForEach(x => x.DefaultChecked = !hasTrue);
            }, _ => ExtensionEnabled && HasSelectedItemOfType<CmdArgument>());

            ResetToDefaultCheckedCommand = new RelayCommand(() =>
            {
                ToolWindowHistory.SaveStateAndPause();
                TreeViewModel.ResetToDefaultChecked();
                ToolWindowHistory.Resume();
            }, _ => ExtensionEnabled && HasSelectedItems());

            EnableExtensionCommand = new RelayCommand(() =>
            {
                lifeCycleService.Value.IsEnabledSaved = true;
                settingsService.Save();
            });
        }

        public void Dispose()
        {
            TreeViewModel.Dispose();
        }

        private bool ExtensionEnabled => lifeCycleService.Value.IsEnabled;

        /// <summary>
        /// Resets the whole state of the tool window view model
        /// </summary>
        public void Reset()
        {
            TreeViewModel.ShowAllProjects = false;
            TreeViewModel.Projects.Clear();
            ToolWindowHistory.Clear();
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
            var selectedItems = TreeViewModel.AllProjects.SelectMany(prj => prj.GetEnumerable(includeSelf: includeProjects)).Where(item => item.IsSelected).ToList();
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
                ToolWindowHistory.SaveState();

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
            return TreeViewModel.SelectedItems.Any();
        }

        /// <summary>
        /// Helper method for CanExecute condition of the commands
        /// </summary>
        /// <returns>True if axactly one line is selected</returns>
        private bool HasSingleSelectedItem()
        {
            return TreeViewModel.SelectedItems.Take(2).Count() == 1;
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
            return HasSingleSelectedItem() && (TreeViewModel.SelectedItems.FirstOrDefault() is T);
        }

        /// <summary>
        /// Helper method for CanExecute condition of the commands
        /// </summary>
        /// <returns>True if axactly one line is selected and it is a argument of the given type.</returns>
        private bool HasSingleSelectedArgumentOfType(ArgumentType argumentType)
        {
            return HasSingleSelectedItem() 
                && TreeViewModel.SelectedItems.FirstOrDefault() is CmdArgument arg 
                && arg.ArgumentType == argumentType;
        }

        /// <summary>
        /// Helper method for CanExecute condition of the commands
        /// </summary>
        /// <returns>True if a valid startup project is set and any line is selected</returns>
        private bool HasStartupProjectAndSelectedItems()
        {
            return HasStartupProject() && HasSelectedItems();
        }

        private IEnumerable<string> ExtractPathFromSelectedArgument()
        {
            var selectedItem = TreeViewModel.SelectedItems.FirstOrDefault();
            if (selectedItem is CmdArgument argument)
            {
                return itemEvaluation.ExtractPathsFromItem(argument);
            }

            return Enumerable.Empty<string>();
        }

        private string ExtractFileNameFromSelectedArgument()
        {
            return ExtractPathFromSelectedArgument().Where(File.Exists).FirstOrDefault();
        }

        private string ExtractDirectoryNameFromSelectedArgument()
        {
            return ExtractPathFromSelectedArgument().Where(Directory.Exists).FirstOrDefault();
        }

        private void RemoveSelectedItems()
        {
            ToolWindowHistory.SaveState();
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

        public void RenameProject(IVsHierarchy project)
        {
            if (TreeViewModel.Projects.TryGetValue(project.GetGuid(), out CmdProject cmdProject))
            {
                cmdProject.Value = project.GetDisplayName();
            }
        }

        public void PopulateFromProjectData(IVsHierarchy project, ProjectDataJson data)
        {
            var guid = project.GetGuid();

            var cmdPrj = new CmdProject(
                guid,
                project.GetKind(),
                project.GetDisplayName(), 
                ListEntriesToCmdObjects(data.Items),
                data.Expanded,
                data.ExclusiveMode,
                data.Delimiter,
                data.Prefix,
                data.Postfix);

            // Assign TreeViewModel after AddRange to not get a lot of ParentChanged events
            cmdPrj.ParentTreeViewModel = TreeViewModel; 

            TreeViewModel.Projects[guid] = cmdPrj;

            cmdPrj.IsSelected = data.Selected;
        }

        public void PopulateFromProjectData(Guid projectId, ProjectDataJson data)
        {
            if (TreeViewModel.Projects.TryGetValue(projectId, out CmdProject cmdPrj))
            {
                cmdPrj.ExclusiveMode = data.ExclusiveMode;
                cmdPrj.IsExpanded = data.Expanded;
                cmdPrj.IsSelected = data.Selected;
                cmdPrj.Delimiter = data.Delimiter;
                cmdPrj.Prefix = data.Prefix;
                cmdPrj.Postfix = data.Postfix;

                cmdPrj.Items.ReplaceRange(ListEntriesToCmdObjects(data.Items));
            }
        }

        private IEnumerable<CmdBase> ListEntriesToCmdObjects(List<CmdArgumentJson> list)
        {
            CmdBase result = null;
            foreach (var item in list)
            {
                if (item.Items == null)
                    result = new CmdArgument(item.Id, item.Type, item.Command, item.Enabled, item.DefaultChecked);
                else
                    result = new CmdGroup(
                        item.Id,
                        item.Command,
                        ListEntriesToCmdObjects(item.Items),
                        item.Expanded,
                        item.ExclusiveMode,
                        item.ProjectConfig,
                        item.ProjectPlatform,
                        item.LaunchProfile,
                        item.Delimiter,
                        item.Prefix,
                        item.Postfix);

                result.IsSelected = item.Selected;
                yield return result;
            }
        }

    }
}
