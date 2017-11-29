using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    public static class DragDrop
    {
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

            if (dropInfo == null)
                dropInfo = new DropInfo();

            if (dropInfo.CouldHadleDrop(e))
            {
                dropInfo.TargetItem = tvItem;
                e.Handled = true;
            }
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

            if (dropInfo != null && dropInfo.CouldHadleDrop(e))
            {
                dropInfo.UpdateInsertPosition(e);
                dropInfo.UpdateTargetCollectionAndIndex();
                if (dropInfo.CanAcceptData(DropInfo.ExtractDropData(dragInfo, e)))
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                        e.Effects = DragDropEffects.Copy;
                    else
                        e.Effects = DragDropEffects.Move;
                }
                else
                    e.Effects = DragDropEffects.None;
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

                foreach (var sourceItem in data)
                {
                    dropInfo.TargetCollection.Insert(idx++, sourceItem);
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

            dropInfo?.UpdateTargetCollectionAndIndex();

            if (result.HasFlag(DragDropEffects.Move))
            {
                foreach (var sourceItem in dragInfo.SourceItems)
                {
                    var sourceCol = sourceItem.Parent.Items;
                    var idx = sourceCol.IndexOf(sourceItem);
                    if (Equals(sourceCol, dropInfo?.TargetCollection) && idx < dropInfo.InsertIndex)
                        dropInfo.InsertIndex--;
                    sourceItem.Parent.Items.RemoveAt(idx);
                }
            }

            if (dropInfo?.TargetItem != null)
                HandleDropForTarget(result);
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
        public TreeViewItemEx DirectVisualSourceItem { get; }
        public TreeViewEx VisualSource { get; }

        public List<TreeViewItemEx> VisualSourceItems { get; private set; }
        public List<CmdBase> SourceItems { get; private set; }

        public bool IsDragInProgress { get; set; }

        public DragInfo(TreeViewItemEx directVisualSourceItem, MouseButtonEventArgs e)
        {
            DragStartPoint = e.GetPosition(directVisualSourceItem);
            DragMouseButton = e.ChangedButton;
            DirectVisualSourceItem = directVisualSourceItem;
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
            return !SourceItems.OfType<CmdProject>().Any();
        }
    }

    public class DropInfo
    {
        private DropTargetAdorner dropTargetAdorner;
        private TreeViewItemEx targetItem;
        private RelativInsertPosition insertPosition;

        public IList<CmdBase> TargetCollection { get; private set; }
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
                             || mousePosition.Y < 0
                             || mousePosition.Y > itemHeight)
                        InsertPosition = RelativInsertPosition.IntoTargetItem;
                    else
                    {
                        InsertPosition = RelativInsertPosition.AfterTargetItem;
                        if (TargetItem.IsExpanded && TargetItem.HasItems)
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

        public void UpdateTargetCollectionAndIndex()
        {
            if (TargetItem == null || InsertPosition == RelativInsertPosition.None)
            {
                TargetCollection = null;
                InsertIndex = 0;
            }
            else
            {
                if (InsertPosition.HasFlag(RelativInsertPosition.IntoTargetItem) && TargetItem.Item is CmdContainer con)
                {
                    TargetCollection = con.Items;
                    InsertIndex = InsertPosition.HasFlag(RelativInsertPosition.AfterTargetItem) ? 0 : TargetCollection.Count;
                }
                else
                {
                    TargetCollection = TargetItem.Item.Parent.Items;
                    InsertIndex = TargetCollection.IndexOf(TargetItem.Item);
                    if (InsertPosition == RelativInsertPosition.AfterTargetItem)
                        InsertIndex++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"UpdateTargetCollectionAndIndex: TargetCollection={TargetCollection}, InsertIndex={InsertIndex}");
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
                .Any(container => Equals(container.Items, TargetCollection)))
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
