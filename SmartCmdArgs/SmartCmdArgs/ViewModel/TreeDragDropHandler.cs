using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;

using SmartCmdArgs.View;
using System.Windows.Input;
using System.Windows.Controls;
using System.ComponentModel;

namespace SmartCmdArgs.ViewModel
{
    class DropHandler : DefaultDropHandler
    {
        private readonly TreeViewModel lvm;

        public DropHandler(TreeViewModel lvm)
        {
            this.lvm = lvm;
        }

        public new bool CanAcceptData(IDropInfo dropInfo)
        {
            if (!DefaultDropHandler.CanAcceptData(dropInfo))
                return false;

            foreach (var dragedTreeViewItem in lvm.DragedTreeViewItems)
            {
                if (dropInfo.VisualTargetItem is TreeViewItemEx
                    && (dropInfo.VisualTargetItem == dragedTreeViewItem
                        || IsChildOf(dropInfo.VisualTargetItem, dragedTreeViewItem)))
                    return false;
            }
            return true;
        }

        public override void DragOver(IDropInfo dropInfo)
        {
            // CmdArgument is not a DragTarget
            if (CanAcceptData(dropInfo))
            {
                if (!(dropInfo.TargetItem is CmdArgument)
                    || !dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter))
                {
                    dropInfo.Effects = ShouldCopyData(dropInfo) ? DragDropEffects.Copy : DragDropEffects.Move;
                    var isTreeViewItem = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) && dropInfo.VisualTargetItem is TreeViewItem;
                    dropInfo.DropTargetAdorner = isTreeViewItem ? DropTargetAdorners.Highlight : DropTargetAdorners.Insert;
                }
            }
        }

        public override void Drop(IDropInfo dropInfo)
        {
            if (dropInfo?.DragInfo == null)
            {
                return;
            }

            var focusedItem = (Keyboard.FocusedElement as TreeViewItemEx)?.Item;

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

            var data = lvm.DragedTreeViewItems.Select(tvItem => tvItem.Item).ToList();

            if (data.Count == 0)
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
                var selectedTreeViewItems = (dropInfo.DragInfo.VisualSource as TreeViewEx)?.SelectedTreeViewItems.ToList();
                bool nothingFocused = true;
                if (selectedTreeViewItems != null)
                {
                    foreach (var selectedTreeViewItem in selectedTreeViewItems)
                    {
                        if (selectedTreeViewItem.Item == focusedItem)
                        {
                            selectedTreeViewItem.Focus();
                            nothingFocused = false;
                        }
                    }
                    if (nothingFocused)
                        selectedTreeViewItems.FirstOrDefault()?.Focus();
                }
            }
        }
    }

    class DragHandler : DefaultDragHandler
    {
        private readonly TreeViewModel lvm;

        public DragHandler(TreeViewModel lvm)
        {
            this.lvm = lvm;
        }

        public override bool CanStartDrag(IDragInfo dragInfo)
        {
            return !((TreeViewEx)dragInfo.VisualSource).SelectedItems.Any(item => item is CmdProject);
        }

        public override void StartDrag(IDragInfo dragInfo)
        {
            var item = dragInfo.SourceItem as CmdBase;

            if (item?.IsEditable == true && item.IsInEditMode)
                item.CommitEdit();

            lvm.DragedTreeViewItems.Clear();

            var selectedTreeViewItems = ((TreeViewEx)dragInfo.VisualSource).SelectedTreeViewItems.ToList();
            var set = new HashSet<CmdBase>(selectedTreeViewItems.Select(x => x.Item));
            var data = selectedTreeViewItems.Where(x => !set.Contains(x.Item.Parent));
            lvm.DragedTreeViewItems.AddRange(data);

            base.StartDrag(dragInfo);
        }
    }
}
