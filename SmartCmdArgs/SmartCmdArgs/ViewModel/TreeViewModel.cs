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
        private ObservableDictionary<Guid, CmdProject> projects;
        private ObservableCollection<CmdProject> startupProjects;
        private object treeitems;
        private bool showAllProjects;

        public ObservableDictionary<Guid, CmdProject> Projects => projects;

        public object TreeItems
        {
            get => treeitems;
            private set
            {
                SetAndNotify(value, ref treeitems);
                TreeItemsView = CollectionViewSource.GetDefaultView(treeitems);
            }
        }

        private ICollectionView treeItemsView;
        public ICollectionView TreeItemsView
        {
            get => treeItemsView;
            private set => SetAndNotify(value, ref treeItemsView);
        }

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

        private CmdBase currentEditingItem;
        private bool _isInEditMode;
        public bool IsInEditMode
        {
            get { return _isInEditMode; }
            set { _isInEditMode = value; OnNotifyPropertyChanged(); }
        }

        public CmdProject FocusedProject => projects.Values.FirstOrDefault(project => project.IsFocusedItem) ?? startupProjects.FirstOrDefault();

        public CmdBase FocusedItem => projects.Values.Concat(projects.Values.SelectMany(prj => prj)).LastOrDefault(prj => prj.IsFocusedItem) ?? startupProjects.FirstOrDefault();

        public ObservableCollection<CmdProject> StartupProjects => startupProjects;

        public List<TreeViewItemEx> DragedTreeViewItems { get; }

        public HashSet<CmdBase> SelectedItems { get; }

        public RelayCommand<int> SelectIndexCommand { get; set; }

        public RelayCommand<object> SelectItemCommand { get; set; }

        public TreeViewModel()
        {
            projects = new ObservableDictionary<Guid, CmdProject>();
            projects.CollectionChanged += OnProjectsChanged;
            treeitems = null;

            startupProjects = new ObservableCollection<CmdProject>();
            startupProjects.CollectionChanged += OnStartupProjectsChanged;

            DragedTreeViewItems = new List<TreeViewItemEx>();

            SelectedItems = new HashSet<CmdBase>();
        }

        private void OnProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var cmdProject in startupProjects.Except(projects.Values).ToList())
                {
                    startupProjects.Remove(cmdProject);
                }

                foreach (var project in projects.Values)
                {
                    project.ParentTreeViewModel = this;
                }
            }
            else
            {
                var inStartupProjects = new Dictionary<Guid, CmdProject>();
                if (e.Action == NotifyCollectionChangedAction.Remove
                    || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.OldItems.Cast<KeyValuePair<Guid, CmdProject>>())
                    {
                        item.Value.ParentTreeViewModel = null;
                        if (e.Action == NotifyCollectionChangedAction.Replace && startupProjects.Contains(item.Value))
                            inStartupProjects.Add(item.Key, item.Value);
                        else if (e.Action == NotifyCollectionChangedAction.Remove)
                            startupProjects.Remove(item.Value);
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Replace
                    || e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var item in e.NewItems.Cast<KeyValuePair<Guid, CmdProject>>())
                    {
                        item.Value.ParentTreeViewModel = this;
                        if (e.Action == NotifyCollectionChangedAction.Replace && inStartupProjects.TryGetValue(item.Key, out CmdProject value))
                            startupProjects[startupProjects.IndexOf(value)] = item.Value;
                    }
                }
            }
        }

        private void OnStartupProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var cmdProject in projects.Values)
                {
                    cmdProject.IsStartupProject = false;
                }

                foreach (var item in startupProjects)
                {
                    item.IsStartupProject = true;
                }
            }
            else
            {
                if (e.Action == NotifyCollectionChangedAction.Remove
                    || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.OldItems.Cast<CmdProject>())
                    {
                        item.IsStartupProject = false;
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.NewItems.Cast<CmdProject>())
                    {
                        item.IsStartupProject = true;
                    }
                }
            }
            
            OnNotifyPropertyChanged(nameof(StartupProjects));
            UpdateTree();
        }

        private void UpdateTree()
        {
            if (ShowAllProjects)
            {
                TreeItems = startupProjects.Concat(projects.Values.Except(startupProjects)
                    .OrderBy(p => p.Value, StringComparer.CurrentCultureIgnoreCase)).ToList();
            }
            else
            {
                if (startupProjects.Count == 1)
                {
                    TreeItems = startupProjects.First().Items;
                }
                else
                {
                    // Fixes a strange potential bug in WPF. If ToList() is missing the project will be shown twice.
                    TreeItems = startupProjects.ToList();
                }
            }

            SelectedItems.ToList().ForEach(item => item.IsSelected = false);
            if (SelectIndexCommand?.CanExecute(0) == true)
                SelectIndexCommand.Execute(0);
        }

        public void AddItemAtFocusedItem(CmdBase item)
        {
            AddItemsAtFocusedItem(new[] {item});
        }

        public void AddItemsAtFocusedItem(IEnumerable<CmdBase> items)
        {
            var focusedItem = FocusedItem;
            if (focusedItem is CmdContainer con && (con.IsExpanded || focusedItem is CmdProject))
            {
                con.Items.InsertRange(0, items);
            }
            else
            {
                var insertIdx = focusedItem.Parent.Items.IndexOf(focusedItem) + 1;
                focusedItem.Parent.Items.InsertRange(insertIdx, items);
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

                foreach (var project in projects.Values)
                {
                    project.Filter = filter;
                }

                TreeItemsView.Refresh();
            });
        }

        public void MoveSelectedEntries(int moveDirection)
        {
            Projects.Values.ForEach(project => project.MoveEntries(SelectedItems, moveDirection));
        }

        public void ToggleSelected()
        {
            var itemsToToggle = SelectedItems.Where(x => !SelectedItems.Contains(x.Parent)).ToList();

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
            if (item.IsSelected)
                SelectedItems.Add(item);
            else
                SelectedItems.Remove(item);

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
                    IsInEditMode = e.IsInEditMode;                   
                    break;
                case ItemsChangedEvent e:
                    // This is called quite frequently, maybe we need
                    // to reduce the number of calls somehow.
                    FireTreeChanged(e);
                    break;
                default:
                    break;
            }
        }
    }
}
