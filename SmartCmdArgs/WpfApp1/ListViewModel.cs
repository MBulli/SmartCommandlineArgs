using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop.Utilities;
using System.Windows.Data;
using System.Collections.Specialized;
using System.Windows.Input;

namespace WpfApp1
{
    public class ListViewModel : PropertyChangedBase
    {
        private ObservableCollection<CmdProject> projects;
        private ObservableCollection<CmdProject> startupProjects;
        private object treeitems;
        private bool showAllProjects;       

        public ObservableCollection<CmdProject> Projects { get => projects; }
        public object TreeItems
        {
            get => treeitems;
            private set => SetAndNotify(value, ref treeitems);
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
        public ObservableCollection<CmdProject> StartupProjects { get => startupProjects; }

        public IDropTarget DropHandler { get; private set; }
        public IDragSource DragHandler { get; private set; }


        public ListViewModel()
        {
            DropHandler = new DropHandler(this);
            DragHandler = new DragHandler(this);

            projects = new ObservableCollection<CmdProject>();
            treeitems = null;
            
            startupProjects = new ObservableCollection<CmdProject>();
            startupProjects.CollectionChanged += OnStartupProjectsChanged;
        }

        private void OnStartupProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove 
                || e.Action == NotifyCollectionChangedAction.Replace 
                || e.Action == NotifyCollectionChangedAction.Reset)
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

            UpdateTree();
        }

        private void UpdateTree()
        {
            if (ShowAllProjects)
            {
                TreeItems = projects;
            }
            else
            {
                if (startupProjects.Count == 1)
                {
                    TreeItems = startupProjects[0].Items;
                }
                else
                {
                    // Fixes a strange potential bug in WPF. If ToList() is missing the project will be shown twice.
                    TreeItems = startupProjects.ToList();
                }
            }
        }
    }
    
    class DropHandler : DefaultDropHandler
    {
        private readonly ListViewModel lvm;

        public DropHandler(ListViewModel lvm)
        {
            this.lvm = lvm;
        }

        public override void DragOver(IDropInfo dropInfo)
        {
            // CmdArgument is not a DragTarget
            base.DragOver(dropInfo);
            if (dropInfo.TargetItem is CmdArgument)
            {
                if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter))
                {
                    dropInfo.DropTargetAdorner = null;
                    dropInfo.Effects = DragDropEffects.None;
                }
                else if (dropInfo.Effects != DragDropEffects.None)
                {
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                }
            }
        }

        public override void Drop(IDropInfo dropInfo)
        {
            if (dropInfo?.DragInfo == null)
            {
                return;
            }

            var focusedItem = (Keyboard.FocusedElement as TreeViewItemEx)?.Header;

            var insertIndex = dropInfo.InsertIndex != dropInfo.UnfilteredInsertIndex ? dropInfo.UnfilteredInsertIndex : dropInfo.InsertIndex;

            var itemsControl = dropInfo.VisualTarget as ItemsControl;
            var editableItems = itemsControl?.Items as IEditableCollectionView;
            if (editableItems != null)
            {
                var newItemPlaceholderPosition = editableItems.NewItemPlaceholderPosition;
                if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning && insertIndex == 0)
                {
                    ++insertIndex;
                }
                else if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtEnd && insertIndex == itemsControl.Items.Count)
                {
                    --insertIndex;
                }
            }
            
            var destinationList = dropInfo.TargetCollection.TryGetList();

            var selectedItems = (dropInfo.DragInfo.VisualSource as TreeViewEx)?.SelectedItems.Cast<CmdBase>().ToList();
            var set = new HashSet<CmdBase>(selectedItems);
            var data = selectedItems.Where(x => !set.Contains(x.Parent)).ToList();


            if (data == null)
              return;

            var copyData = ShouldCopyData(dropInfo);
            if (!copyData)
            {
                foreach (var o in data)
                {
                    var sourceList = o.Parent.Items;
                    var index = sourceList.IndexOf(o);
                    if (index != -1)
                    {
                        sourceList.RemoveAt(index);
                        // so, is the source list the destination list too ?
                        if (destinationList != null && Equals(sourceList, destinationList) && index < insertIndex)
                        {
                            --insertIndex;
                        }
                    }
                }
            }

            if (destinationList != null)
            {
                // check for cloning
                var cloneData = dropInfo.Effects.HasFlag(DragDropEffects.Copy)
                                || dropInfo.Effects.HasFlag(DragDropEffects.Link);
                foreach (var o in data)
                {
                    var obj2Insert = o;
                    if (cloneData)
                    {
                        var cloneable = o as ICloneable;
                        if (cloneable != null)
                        {
                            obj2Insert = cloneable.Clone() as CmdBase;
                        }
                    }

                    destinationList.Insert(insertIndex++, obj2Insert);
                }
            }

            // focus and selection handling after an item has ben droped
            if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter)
                && dropInfo.TargetItem is CmdContainer
                && !((TreeViewItemEx)dropInfo.VisualTargetItem).IsExpanded)
            {
                dropInfo.VisualTargetItem.Focus();
                var container = (CmdContainer)dropInfo.TargetItem;
                container.IsSelected = true;
                container.SetIsSelectedOnChildren(false);
            }
            else
            {
                var selectedTreeViewItems = (dropInfo.DragInfo.VisualSource as TreeViewEx)?.SelectedTreeViewItems.Cast<TreeViewItemEx>().ToList();
                bool nothingFocused = true;
                if (selectedTreeViewItems != null)
                {
                    foreach (var selectedTreeViewItem in selectedTreeViewItems)
                    {
                        if (selectedTreeViewItem.Header == focusedItem)
                        {
                            selectedTreeViewItem.Focus();
                            nothingFocused = false;
                        }
                    }
                }
                if (nothingFocused)
                    selectedTreeViewItems.FirstOrDefault()?.Focus();
            }
        }
    }

    class DragHandler : DefaultDragHandler
    {
        private readonly ListViewModel lvm;

        public DragHandler(ListViewModel lvm)
        {
            this.lvm = lvm;
        }

        public override bool CanStartDrag(IDragInfo dragInfo)
        {
            return !((TreeViewItemEx)dragInfo.VisualSourceItem).ParentTreeView.SelectedItems.Cast<CmdBase>().Any(item => item is CmdProject);
        }

        public override void StartDrag(IDragInfo dragInfo)
        {
            var item = dragInfo.SourceItem as CmdBase;

            if (item?.IsEditable == true && item.IsInEditMode)
                item.CommitEdit();

            base.StartDrag(dragInfo);
        }
    }

}
