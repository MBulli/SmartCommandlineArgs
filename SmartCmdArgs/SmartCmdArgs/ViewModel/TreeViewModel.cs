using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using SmartCmdArgs.Helper;
using SmartCmdArgs.View;

namespace SmartCmdArgs.ViewModel
{
    public class TreeViewModel : PropertyChangedBase
    {
        public ObservableDictionary<Guid, CmdProject> Projects { get; }

        private IEnumerable<CmdBase> treeitems;
        public IEnumerable<CmdBase> TreeItems
        {
            get => treeitems;
            private set
            {
                treeitems = value;
                TreeItemsView = CollectionViewSource.GetDefaultView(treeitems);
            }
        }

        private ICollectionView treeItemsView;
        public ICollectionView TreeItemsView
        {
            get => treeItemsView;
            private set => SetAndNotify(value, ref treeItemsView);
        }

        private bool showAllProjects;
        public bool ShowAllProjects
        {
            get => showAllProjects;
            set
            {
                if (showAllProjects != value)
                {
                    SetAndNotify(value, ref showAllProjects);
                    UpdateTree();
                }
            }
        }

        private string textBeforeEdit;
        private CmdBase currentEditingItem;
        private bool _isInEditMode;
        public bool IsInEditMode
        {
            get { return _isInEditMode; }
            set { _isInEditMode = value; OnNotifyPropertyChanged(); }
        }

        public CmdProject FocusedProject => AllProjects.FirstOrDefault(project => project.IsFocusedItem) ?? StartupProjects.FirstOrDefault();

        public CmdBase FocusedItem
        {
            get
            {
                return IterateOnlyFocused(TreeItems).LastOrDefault() ?? StartupProjects.FirstOrDefault();

                IEnumerable<CmdBase> IterateOnlyFocused(IEnumerable<CmdBase> items)
                {
                    if (items == null)
                        yield break;

                    foreach (var item in items)
                    {
                        if (item.IsFocusedItem)
                        {
                            yield return item;
                            if (item is CmdContainer con && con.IsExpanded)
                            {
                                foreach (var conItem in IterateOnlyFocused(con.Items))
                                {
                                    yield return conItem;
                                }
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<CmdProject> AllProjects => Projects.Values;

        public IEnumerable<CmdProject> StartupProjects => AllProjects.Where(p => p.IsStartupProject);

        public IEnumerable<CmdBase> AllItems => AllProjects.Concat(AllProjects.SelectMany(p => p));

        public IEnumerable<CmdArgument> AllArguments => AllProjects.SelectMany(p => p.AllArguments);

        public IEnumerable<CmdBase> SelectedItems => AllItems.Where(item => item.IsSelected);


        public List<TreeViewItemEx> DragedTreeViewItems { get; }

        public RelayCommand<int> SelectIndexCommand { get; set; }

        public RelayCommand<object> SelectItemCommand { get; set; }

        public TreeViewModel()
        {
            Projects = new ObservableDictionary<Guid, CmdProject>();
            Projects.CollectionChanged += OnProjectsChanged;
            treeitems = null;

            DragedTreeViewItems = new List<TreeViewItemEx>();
        }

        private void OnProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var project in AllProjects)
                {
                    project.ParentTreeViewModel = this;
                }
            }
            else
            {
                var replacedStartupProjects = new HashSet<Guid>();
                if (e.Action == NotifyCollectionChangedAction.Remove
                    || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.OldItems.Cast<KeyValuePair<Guid, CmdProject>>())
                    {
                        item.Value.ParentTreeViewModel = null;
                        if (e.Action == NotifyCollectionChangedAction.Replace && item.Value.IsStartupProject)
                            replacedStartupProjects.Add(item.Key);
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Replace
                    || e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var item in e.NewItems.Cast<KeyValuePair<Guid, CmdProject>>())
                    {
                        item.Value.ParentTreeViewModel = this;
                        if (replacedStartupProjects.Contains(item.Key))
                            item.Value.IsStartupProject = true;
                    }
                }
            }
            UpdateTree();
        }
        
        public void UpdateTree()
        {
            // reset focus
            foreach (var item in AllProjects.SelectMany(x => x.GetEnumerable(useView: false, includeSelf: true)))
            {
                item.IsFocusedItem = false;
            }

            if (ShowAllProjects)
            {
                TreeItems = AllProjects
                    .GroupBy(p => p.IsStartupProject).OrderByDescending(g => g.Key)
                    .SelectMany(g => g.OrderBy(p => p.Value, StringComparer.CurrentCultureIgnoreCase))
                    .ToList();
            }
            else
            {
                var startupProjects = StartupProjects.ToList();
                if (startupProjects.Count == 1)
                {
                    var project = startupProjects.First();

                    // set the project as focused because it does not appear in the tree (UI)
                    // and therfore it cant be set as focused by the UI
                    project.IsFocusedItem = true;

                    // selected must be false because it isn't set to false by the tree (UI)
                    // and if it would be true, then for each action where selected
                    // elements are used, all elements of this project would be used
                    project.IsSelected = false;

                    TreeItems = project.Items;
                }
                else
                {
                    // Fixes a strange potential bug in WPF. If ToList() is missing the project will be shown twice.
                    TreeItems = startupProjects.ToList();
                }

                // reset selected
                foreach (var item in AllProjects.Where(x => !x.IsStartupProject).SelectMany(x => x.GetEnumerable(useView: false, includeSelf: true)))
                {
                    item.IsSelected = false;
                }
            }

            if (!SelectedItems.Any())
                SelectIndexCommand?.SafeExecute(0);
        }

        public void SelectItems(IEnumerable<CmdBase> items)
        {
            var isFirst = true;
            foreach (CmdBase item in items)
            {
                if (isFirst)
                  SelectItemCommand.SafeExecute(item);
                else
                  item.IsSelected = true;
                
                isFirst = false;
            }
        }

        public void AddItemAtFocusedItem(CmdBase item)
        {
            AddItemsAtFocusedItem(new[] {item});
        }

        public void AddItemsAtFocusedItem(IEnumerable<CmdBase> items)
        {
            AddItemsAt(FocusedItem, items);
        }

        public void AddItemsAt(CmdBase targetItem, IEnumerable<CmdBase> items)
        {
            if (targetItem is CmdContainer con && (con.IsExpanded || targetItem is CmdProject))
            {
                // make sure the container is expanded, so the selection works and the user knows what's happening
                con.IsExpanded = true;

                con.InsertRange(0, items);
            }
            else
            {
                var insertIdx = targetItem.Parent.Items.IndexOf(targetItem) + 1;
                targetItem.Parent.InsertRange(insertIdx, items);
            }
        }

        public void SetStringFilter(string filterString, bool matchCase = false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Predicate<CmdBase> filter = null;
                if (!string.IsNullOrEmpty(filterString))
                {
                    filter = item => 
                           item is CmdArgument && item.Value.Contains(filterString, matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase) 
                        || item is CmdContainer && !((CmdContainer)item).ItemsView.IsEmpty;
                }

                foreach (var project in AllProjects)
                {
                    project.Filter = filter;
                }

                TreeItemsView?.Refresh();
            });
        }

        public void MoveSelectedEntries(int moveDirection)
        {
            if (SelectedItems.Any()) {
                AllProjects.ForEach(project => project.MoveSelectedEntries(moveDirection));
            }
        }

        public void ToggleSelected()
        {
            var selectedItemsSet = new HashSet<CmdBase>(SelectedItems);
            var itemsToToggle = selectedItemsSet.Where(x => !selectedItemsSet.Contains(x.Parent)).ToList();

            var checkState = itemsToToggle.All(item => item.IsChecked == false);
            foreach (var selectedItem in itemsToToggle)
            {
                selectedItem.IsChecked = checkState;
            }
        }

        public void CancelEditMode()
        {
            if (IsInEditMode)
            {
                currentEditingItem?.CancelEdit();
            }
        }

        public class TreeChangedEventArgs : EventArgs
        {
            public CmdBase Source { get; private set; }
            public CmdProject AffectedProject { get; private set; }
            
            public TreeChangedEventArgs(CmdBase source, CmdProject affectedProject)
            {
                Source = source;
                AffectedProject = affectedProject;
            }
        }

        public event EventHandler<CmdBase> ItemSelectionChanged;
        public event EventHandler<TreeChangedEventArgs> TreeContentChanged;

        public virtual void OnItemSelectionChanged(CmdBase item)
        {
            ItemSelectionChanged?.Invoke(this, item);
        }

        public void OnTreeEvent(TreeEventBase treeEvent)
        {
            void FireTreeChanged(TreeEventBase e)
            {
                TreeContentChanged?.Invoke(this, new TreeChangedEventArgs(e.Sender, e.AffectedProject));
            }
            
            switch (treeEvent)
            {
                case SelectionChangedEvent e:
                    OnItemSelectionChanged(e.Sender);
                    break;
                case ParentChangedEvent e:
                    FireTreeChanged(e);
                    break;
                case ValueChangedEvent e:
                    FireTreeChanged(e);
                    break;
                case CheckStateChangedEvent e:
                    break;
                case ItemEditModeChangedEvent e:
                    currentEditingItem = e.IsInEditMode ? e.Sender : null;
                    if (currentEditingItem != null)
                    {
                        textBeforeEdit = currentEditingItem.Value;
                        ToolWindowHistory.SaveState();
                    }
                    else if (textBeforeEdit == e.Sender.Value)
                    {
                        ToolWindowHistory.DeleteNewest();
                        textBeforeEdit = null;
                    }

                    IsInEditMode = e.IsInEditMode;                   
                    break;
                case ItemsChangedEvent e:
                    // This is called quite frequently, maybe we need
                    // to reduce the number of calls somehow.
                    FireTreeChanged(e);
                    break;
                case ProjectConfigChangedEvent e:
                    FireTreeChanged(e);
                    break;
                case LaunchProfileChangedEvent e:
                    FireTreeChanged(e);
                    break;
                case ExclusiveModeChangedEvent e:
                    FireTreeChanged(e);
                    break;
                case DelimiterChangedEvent e:
                    FireTreeChanged(e);
                    break;
                default:
                    break;
            }
        }
    }
}
