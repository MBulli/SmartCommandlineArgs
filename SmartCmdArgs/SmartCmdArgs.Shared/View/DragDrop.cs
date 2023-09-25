using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Services;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    public static class DragDrop
    {
        private static IToolWindowHistory ToolWindowHistory => CmdArgsPackage.Instance.ServiceProvider.GetRequiredService<IToolWindowHistory>();

        private static DragInfo dragInfo;
        private static DropInfo dropInfo;

        public static void OnMouseDown(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed
                && (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right))
            {
                dragInfo = new DragInfo(tvItem, e);
            }
        }

        public static void OnMouseMove(TreeViewEx treeView, MouseEventArgs e)
        {
            if (dragInfo == null)
                return;

            if (dragInfo.ShouldCancel(e))
            {
                dragInfo = null;
            }
            else if (dragInfo.SouldStartDrag(e))
            {
                dragInfo.GatherSelectedItems(treeView);
                if (dragInfo.CanStartDrag())
                {
                    var dataObject = DataObjectGenerator.Genrate(dragInfo.SourceItems, includeObject: true);

                    if (dragInfo.DirectVisualSourceItem.Item.IsInEditMode)
                        dragInfo.DirectVisualSourceItem.Item.CommitEdit();

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"StartDrag: {dragInfo.DirectVisualSourceItem.Item}");
                        dragInfo.IsDragInProgress = true;
                        var result = System.Windows.DragDrop.DoDragDrop(treeView, dataObject, DragDropEffects.Move | DragDropEffects.Copy);
                        dragInfo.IsDragInProgress = false;
                        if (result != DragDropEffects.None)
                            HandleDropForSource(result);
                    }
                    finally
                    {
                        Cancel();
                    }
                }
            }
        }

        public static void OnDragEnter(TreeViewItemEx tvItem, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"DragEnter: {tvItem.Item}");

            // this logic was moved to OnDragOver to fix an issue 
            // when using drag and drop if the window is floating

            //if (dropInfo == null)
            //    dropInfo = new DropInfo();

            //if (dropInfo.CouldHadleDrop(e))
            //{
            //    dropInfo.TargetItem = tvItem;
            //    e.Handled = true;
            //}

            OnDragOver(tvItem, e);
        }

        public static void OnQueryContinueDrag(TreeViewItemEx tvItem, QueryContinueDragEventArgs e)
        {
            if (e.Action == DragAction.Cancel || e.EscapePressed)
            {
                Cancel();
                e.Handled = true;
            }
        }

        public static void OnDragOver(TreeViewItemEx tvItem, DragEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"DragOver: {tvItem.Item}");

            if (dropInfo == null)
                dropInfo = new DropInfo();

            if (dropInfo.CouldHadleDrop(e))
            {
                // set the target item, this also initializes the DropTargetAdorner
                dropInfo.TargetItem = tvItem;

                // update data related to the current mouse position and the target item
                dropInfo.UpdateInsertPosition(e);
                dropInfo.UpdateTargetContainerAndIndex();

                // set the drop mode Copy|Move|None
                if (dropInfo.CanAcceptData(DropInfo.ExtractDropData(dragInfo, e)))
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        e.Effects = DragDropEffects.Copy;
                    else
                        e.Effects = DragDropEffects.Move;
                }
                else
                    e.Effects = DragDropEffects.None;

                // this controls the adorner
                dropInfo.Effects = e.Effects;
                
                e.Handled = true;
            }
        }

        public static void OnDragLeave(TreeViewItemEx tvItem, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"DragLeave: {tvItem.Item}");
            if (dropInfo != null)
                dropInfo.TargetItem = null;
        }

        public static void HandleDropForTarget(TreeViewItemEx tvItem, DragEventArgs e)
        {
            OnDragOver(tvItem, e);
            if (dragInfo?.IsDragInProgress == true)
                return;

            HandleDropForTarget(e.Effects, e);
        }

        public static void HandleDropForTarget(DragDropEffects result, DragEventArgs e = null)
        {
            System.Diagnostics.Debug.WriteLine($"HandleDropForTarget: {dropInfo.TargetItem.Item}");
            IEnumerable<CmdBase> data = DropInfo.ExtractDropData(dragInfo, e);
            if (dropInfo.CanAcceptData(data)
                && (result.HasFlag(DragDropEffects.Move) || result.HasFlag(DragDropEffects.Copy)))
            {
                var idx = dropInfo.InsertIndex;
                if (result.HasFlag(DragDropEffects.Copy))
                    data = data.Select(cmd => cmd.Copy());

                var dataList = data.ToList();
                
                var souldDeselctItem = dropInfo.InsertPosition.HasFlag(DropInfo.RelativInsertPosition.IntoTargetItem) 
                    && dropInfo.TargetItem.Item is CmdContainer tarCon 
                    && !tarCon.IsExpanded;

                if (dataList.Count > 0)
                    ToolWindowHistory.SaveState();

                foreach (var sourceItem in dataList)
                {
                    if (souldDeselctItem)
                    {
                        if (sourceItem is CmdContainer con)
                        {
                            con.GetEnumerable(useView: false, includeSelf: true)
                                .ForEach(item => item.IsSelected = false);
                        }
                        else
                        {
                            sourceItem.IsSelected = false;
                        }
                    }

                    dropInfo.TargetContainer.Insert(idx++, sourceItem);
                }

                var focusItem = dragInfo?.DirectSourceItem ?? dataList.FirstOrDefault();

                var selectItemCommand = dropInfo.TargetItem.ParentTreeView.SelectItemCommand;
                if (souldDeselctItem)
                    selectItemCommand.SafeExecute(dropInfo.TargetItem.Item);
                else if (selectItemCommand.SafeExecute(focusItem))
                {
                    foreach (var sourceItem in dataList)
                    {
                        sourceItem.IsSelected = true;
                    }
                }
            }
            else
            {
                if (e != null) e.Effects = DragDropEffects.None;
            }

            dropInfo?.DropTargetAdorner?.Detach();
            dropInfo = null;
        }

        private static void HandleDropForSource(DragDropEffects result)
        {
            System.Diagnostics.Debug.WriteLine($"HandleDropForSource: {result}");

            dropInfo?.UpdateTargetContainerAndIndex();

            if (result.HasFlag(DragDropEffects.Move))
            {
                ToolWindowHistory.SaveStateAndPause();

                foreach (var sourceItem in dragInfo.SourceItems)
                {
                    var sourceCol = sourceItem.Parent;
                    var idx = sourceCol.Items.IndexOf(sourceItem);
                    if (Equals(sourceCol, dropInfo?.TargetContainer) && idx < dropInfo.InsertIndex)
                        dropInfo.InsertIndex--;
                    sourceItem.Parent.Items.RemoveAt(idx);
                }
            }

            if (dropInfo?.TargetItem != null)
                HandleDropForTarget(result);

            if (result.HasFlag(DragDropEffects.Move))
            {
                ToolWindowHistory.Resume();
            }
        }

        private static void Cancel()
        {
            dropInfo?.DropTargetAdorner?.Detach();
            dropInfo = null;
            dragInfo = null;
        }
    }

    public class DragInfo
    {
        public Point DragStartPoint { get; }
        public MouseButton DragMouseButton { get; }
        public TreeViewEx VisualSource { get; }

        public TreeViewItemEx DirectVisualSourceItem { get; }
        public CmdBase DirectSourceItem { get; }

        public List<TreeViewItemEx> VisualSourceItems { get; private set; }
        public List<CmdBase> SourceItems { get; private set; }

        public bool IsDragInProgress { get; set; }

        public DragInfo(TreeViewItemEx directVisualSourceItem, MouseButtonEventArgs e)
        {
            DragStartPoint = e.GetPosition(directVisualSourceItem);
            DragMouseButton = e.ChangedButton;
            DirectVisualSourceItem = directVisualSourceItem;
            DirectSourceItem = directVisualSourceItem.Item;
            VisualSource = directVisualSourceItem.ParentTreeView;
        }

        public void GatherSelectedItems(TreeViewEx treeView)
        {
            var selectedTreeViewItems = treeView.SelectedTreeViewItems.ToList();
            var set = new HashSet<CmdBase>(selectedTreeViewItems.Select(x => x.Item));
            VisualSourceItems = selectedTreeViewItems.Where(x => !set.Contains(x.Item.Parent)).ToList();
            SourceItems = VisualSourceItems.Select(tvItem => tvItem.Item).ToList();
        }

        public bool ShouldCancel(MouseEventArgs e)
        {
            if (DragMouseButton == MouseButton.Left && e.LeftButton == MouseButtonState.Released)
                return true;
            if (DragMouseButton == MouseButton.Right && e.LeftButton == MouseButtonState.Released)
                return true;
            return false;
        }

        public bool SouldStartDrag(MouseEventArgs e)
        {
            Point curPos = e.GetPosition(DirectVisualSourceItem);
            return Math.Abs(curPos.X - DragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance
                   || Math.Abs(curPos.Y - DragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance;
        }

        public bool CanStartDrag()
        {
            return DirectVisualSourceItem?.Item != null &&
                !SourceItems.OfType<CmdProject>().Any();
        }
    }

    public class DropInfo
    {
        private DropTargetAdorner dropTargetAdorner;
        private TreeViewItemEx targetItem;
        private RelativInsertPosition insertPosition;

        public CmdContainer TargetContainer { get; private set; }
        public int InsertIndex { get; set; }
        public DragDropEffects Effects { get; set; }

        public DropTargetAdorner DropTargetAdorner
        {
            get => dropTargetAdorner;
            private set
            {
                dropTargetAdorner?.Detach();
                dropTargetAdorner = value;
            }
        }

        public TreeViewItemEx TargetItem
        {
            get => targetItem;
            set
            {
                if (targetItem == value) return;
                targetItem = value;
                DropTargetAdorner = value != null ? new DropTargetAdorner(value, this) : null;

                System.Diagnostics.Debug.WriteLine($"Updated TargetItem: {targetItem}");
            }
        }

        public RelativInsertPosition InsertPosition
        {
            get => insertPosition;
            private set
            {
                insertPosition = value;
                DropTargetAdorner?.InvalidateVisual();
            }
        }

        public void UpdateInsertPosition(DragEventArgs e)
        {
            if (TargetItem != null)
            {
                var mousePosition = e.GetPosition(TargetItem.HeaderBorder);
                var itemHeight = TargetItem.HeaderBorder.RenderSize.Height;
                if (TargetItem.Item is CmdProject prj)
                {
                    InsertPosition = RelativInsertPosition.IntoTargetItem;
                    if (prj.IsExpanded && mousePosition.Y > itemHeight * 0.75 && mousePosition.Y <= itemHeight)
                        InsertPosition |= RelativInsertPosition.AfterTargetItem;
                }
                else if (TargetItem.Item is CmdContainer)
                {
                    if (mousePosition.Y < itemHeight * 0.25)
                        InsertPosition = RelativInsertPosition.BeforeTargetItem;
                    else if (mousePosition.Y < itemHeight * 0.75
                             || mousePosition.Y < 0)
                        InsertPosition = RelativInsertPosition.IntoTargetItem;
                    else
                    {
                        InsertPosition = RelativInsertPosition.AfterTargetItem;
                        if (TargetItem.IsExpanded && TargetItem.HasItems && mousePosition.Y <= itemHeight)
                            InsertPosition |= RelativInsertPosition.IntoTargetItem;
                    }
                }
                else
                {
                    if (mousePosition.Y < itemHeight * 0.5)
                        InsertPosition = RelativInsertPosition.BeforeTargetItem;
                    else
                        InsertPosition = RelativInsertPosition.AfterTargetItem;
                }
            }
            else
                InsertPosition = RelativInsertPosition.None;
        }

        public void UpdateTargetContainerAndIndex()
        {
            if (TargetItem == null || InsertPosition == RelativInsertPosition.None)
            {
                TargetContainer = null;
                InsertIndex = 0;
            }
            else
            {
                if (InsertPosition.HasFlag(RelativInsertPosition.IntoTargetItem) && TargetItem.Item is CmdContainer con)
                {
                    TargetContainer = con;
                    InsertIndex = InsertPosition.HasFlag(RelativInsertPosition.AfterTargetItem) ? 0 : TargetContainer.Items.Count;
                }
                else
                {
                    TargetContainer = TargetItem.Item.Parent;
                    InsertIndex = TargetContainer.Items.IndexOf(TargetItem.Item);
                    if (InsertPosition == RelativInsertPosition.AfterTargetItem)
                        InsertIndex++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"UpdateTargetContainerAndIndex: TargetContainer={TargetContainer}, InsertIndex={InsertIndex}");
        }

        public bool CouldHadleDrop(DragEventArgs e)
        {
            return DataObjectGenerator.ExtractableDataPresent(e?.Data);
        }

        public bool CanAcceptData(IEnumerable<CmdBase> data)
        {
            if (data == null)
                return false;

            var sourceContainerItems = data.OfType<CmdContainer>().ToList();
            if (sourceContainerItems.Concat(sourceContainerItems.SelectMany(container => container.AllContainer))
                .Any(container => Equals(container, TargetContainer)))
                return false;

            return true;
        }

        public static List<CmdBase> ExtractDropData(DragInfo dragInfo, DragEventArgs e)
        {
            return dragInfo?.SourceItems ?? DataObjectGenerator.Extract(e?.Data, includeObject: true)?.ToList();
        }

        [Flags]
        public enum RelativInsertPosition
        {
            None = 0,
            BeforeTargetItem = 1,
            AfterTargetItem = 2,
            IntoTargetItem = 4
        }
    }
}
