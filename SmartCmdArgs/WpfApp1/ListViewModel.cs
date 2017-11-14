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

namespace WpfApp1
{
    public class ListViewModel : PropertyChangedBase
    {
        private ObservableCollection<CmdProject> projects;

        public ObservableCollection<CmdProject> Projects { get => projects; }

        public IDropTarget DropHandler { get; } = new DropHandler();
        public IDragSource DragHandler { get; } = new DragHandler();


        public ListViewModel()
        {
            projects = new ObservableCollection<CmdProject>();
        }

    }

    class DropHandler : DefaultDropHandler
    {
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
            var data = (dropInfo.DragInfo.VisualSource as TreeViewEx)?.SelectedItems.Cast<ICmdItem>().ToList();
            
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
                            obj2Insert = cloneable.Clone() as ICmdItem;
                        }
                    }

                    destinationList.Insert(insertIndex++, obj2Insert);
                }
            }
        }
    }

    class DragHandler : DefaultDragHandler
    {
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
