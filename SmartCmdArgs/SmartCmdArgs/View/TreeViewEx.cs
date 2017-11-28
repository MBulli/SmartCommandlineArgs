using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using GongSolutions.Wpf.DragDrop.Utilities;
using Microsoft.VisualStudio.Composition.Tasks;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    public class TreeViewEx : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx(this);
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        // taken from https://stackoverflow.com/questions/459375/customizing-the-treeview-to-allow-multi-select

        // Used in shift selections
        private TreeViewItemEx _lastItemSelected;

        public static readonly DependencyProperty IsItemSelectedProperty =
            DependencyProperty.RegisterAttached("IsItemSelected", typeof(bool), typeof(TreeViewEx));

        public static void SetIsItemSelected(UIElement element, bool value)
        {
            element.SetValue(IsItemSelectedProperty, value);
        }
        public static bool GetIsItemSelected(UIElement element)
        {
            return (bool)element.GetValue(IsItemSelectedProperty);
        }
        

        private static bool IsCtrlPressed => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        private static bool IsShiftPressed => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        public IEnumerable<TreeViewItemEx> SelectedTreeViewItems => GetTreeViewItems(this, true).Where(GetIsItemSelected);
        public IEnumerable<CmdBase> SelectedItems => SelectedTreeViewItems.Select(treeViewItem => treeViewItem.Item);
        
        
        public void ChangedFocusedItem(TreeViewItemEx item)
        {
            if (Keyboard.IsKeyDown(Key.Up)
                || Keyboard.IsKeyDown(Key.Down)
                || Keyboard.IsKeyDown(Key.Left)
                || Keyboard.IsKeyDown(Key.Right)
                || Keyboard.IsKeyDown(Key.Prior)
                || Keyboard.IsKeyDown(Key.Next)
                || Keyboard.IsKeyDown(Key.End)
                || Keyboard.IsKeyDown(Key.Home))
            {
                SelectedItemChangedInternal(item);
            }
        }

        private TreeViewItemEx _lastMouseDownTargetItem;
        public void MouseLeftButtonDownOnItem(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            _lastMouseDownTargetItem = tvItem;
            if (IsCtrlPressed || IsShiftPressed || !SelectedItems.Skip(1).Any() || !GetIsItemSelected(tvItem))
            {
                SelectedItemChangedInternal(tvItem);
            }
        }

        public void MouseLeftButtonUpOnItem(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            if (IsCtrlPressed || IsShiftPressed || !SelectedItems.Skip(1).Any())
                return;

            if (!Equals(tvItem, _lastMouseDownTargetItem))
                return;

            SelectedItemChangedInternal(tvItem);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            foreach (var treeViewItem in GetTreeViewItems(this, false))
            {
                var cmdItem = treeViewItem.Item;
                if (cmdItem.IsInEditMode)
                {
                    cmdItem.CommitEdit();
                    e.Handled = true;
                }
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.A && e.IsDown && IsCtrlPressed)
            {
                foreach (var treeViewItem in GetTreeViewItems(this, false))
                {
                    SetIsItemSelected(treeViewItem, true);
                }
                e.Handled = true;
            }
        }
        
        private void SelectedItemChangedInternal(TreeViewItemEx tvItem)
        {
            // Clear all previous selected item states if ctrl is NOT being held down
            if (!IsCtrlPressed)
            {
                foreach (var treeViewItem in GetTreeViewItems(this, true))
                    SetIsItemSelected(treeViewItem, false);
            }
            
            // Is this an item range selection?
            if (IsShiftPressed && _lastItemSelected != null)
            {
                var items = GetTreeViewItemRange(_lastItemSelected, tvItem);
                if (items.Count > 0)
                {
                    foreach (var treeViewItem in items)
                        SetIsItemSelected(treeViewItem, true);

                    //_lastItemSelected = items.Last();
                }
            }
            // Otherwise, individual selection (toggle if CTRL is Pressed)
            else
            {
                SetIsItemSelected(tvItem, !IsCtrlPressed || !GetIsItemSelected(tvItem));
                _lastItemSelected = tvItem;
            }
        }
        private static IEnumerable<TreeViewItemEx> GetTreeViewItems(ItemsControl parentItem, bool includeCollapsedItems, List<TreeViewItemEx> itemList = null)
        {
            if (itemList == null)
                itemList = new List<TreeViewItemEx>();

            for (var index = 0; index < parentItem.Items.Count; index++)
            {
                var tvItem = parentItem.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItemEx;
                if (tvItem == null) continue;

                yield return tvItem;
                if (includeCollapsedItems || tvItem.IsExpanded)
                {
                    foreach (var item in GetTreeViewItems(tvItem, includeCollapsedItems, itemList))
                        yield return item;
                }
            }
        }
        private List<TreeViewItemEx> GetTreeViewItemRange(TreeViewItemEx start, TreeViewItemEx end)
        {
            var items = GetTreeViewItems(this, false).ToList();

            var startIndex = items.IndexOf(start);
            var endIndex = items.IndexOf(end);
            var rangeStart = startIndex > endIndex || startIndex == -1 ? endIndex : startIndex;
            var rangeCount = startIndex > endIndex ? startIndex - endIndex + 1 : endIndex - startIndex + 1;

            if (startIndex == -1 && endIndex == -1)
                rangeCount = 0;

            else if (startIndex == -1 || endIndex == -1)
                rangeCount = 1;

            return rangeCount > 0 ? items.GetRange(rangeStart, rangeCount) : new List<TreeViewItemEx>();
        }
        
        protected override void OnMouseMove(MouseEventArgs e) => DragDrop.OnMouseMove(this, e);
    }

    public class TreeViewItemEx : TreeViewItem
    {
        private readonly Lazy<FrameworkElement> headerBorder;
        public FrameworkElement HeaderBorder => headerBorder.Value;

        public CmdBase Item => DataContext as CmdBase;

        private static bool IsCtrlPressed => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        private static bool IsShiftPressed => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        public TreeViewEx ParentTreeView { get; }

        public int Level
        {
            get { return (int)GetValue(LevelProperty); }
            set { SetValue(LevelProperty, value); }
        }

        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx(ParentTreeView, this.Level+1);
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        public TreeViewItemEx(TreeViewEx parentTreeView, int level = 0)
        {
            ParentTreeView = parentTreeView;
            Level = level;

            headerBorder = new Lazy<FrameworkElement>(() => (FrameworkElement)GetTemplateChild("HeaderBorder"));

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindingOperations.ClearBinding(this, TreeViewEx.IsItemSelectedProperty);
            BindingOperations.ClearBinding(this, TreeViewEx.IsItemSelectedProperty);

            if (e.NewValue is CmdBase)
            {
                Binding bind = new Binding
                {
                    Source = e.NewValue,
                    Path = new PropertyPath(nameof(CmdBase.IsSelected)),
                    Mode = BindingMode.TwoWay
                };
                SetBinding(TreeViewEx.IsItemSelectedProperty, bind);
            }

            if (e.NewValue is CmdContainer)
            {
                Binding bind = new Binding
                {
                    Source = e.NewValue,
                    Path = new PropertyPath(nameof(CmdContainer.IsExpanded)),
                    Mode = BindingMode.TwoWay
                };
                SetBinding(IsExpandedProperty, bind);
            }
        }
        
        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            if (IsFocused 
                && Item.IsEditable 
                && !Item.IsInEditMode 
                && !string.IsNullOrEmpty(e.Text)
                && !char.IsControl(e.Text[0]))
            {
                 Item.BeginEdit(initialValue: e.Text);
            }

            base.OnTextInput(e);
        }


        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (IsFocused)
            {
                var selectedItems = ParentTreeView.SelectedItems.ToList();
                if (e.Key == Key.Space && !selectedItems.Any(item => item.IsInEditMode))
                {
                    bool select = selectedItems.All(item => item.IsChecked == false);
                    foreach (var selectedItem in selectedItems)
                    {
                        selectedItem.IsChecked = select;
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Return || e.Key == Key.F2)
                {
                    if (Item.IsEditable && !Item.IsInEditMode)
                    {
                        Item.BeginEdit();
                        e.Handled = true;
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            ParentTreeView.MouseLeftButtonDownOnItem(this, e);

            if (IsFocused 
                && Item.IsEditable 
                && !Item.IsInEditMode 
                && !IsCtrlPressed 
                && ParentTreeView.SelectedItems.Take(2).Count() == 1 
                && (e.ClickCount % 2 == 1 || !(Item is CmdContainer)))
            {
                Item.BeginEdit();
            }
            else
            {
                if (e.ClickCount % 2 == 0 && Item is CmdContainer)
                    IsExpanded = !IsExpanded;

                Focus();
            }
            e.Handled = true;

            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            ParentTreeView.MouseLeftButtonUpOnItem(this, e);
            e.Handled = true;
        }

        protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((bool) e.NewValue)
                ParentTreeView.ChangedFocusedItem(this);
        }

        protected override void OnExpanded(RoutedEventArgs e)
        {
            if (Item.IsEditable && Item.IsInEditMode)
                Item.CommitEdit();

            base.OnExpanded(e);
        }

        protected override void OnCollapsed(RoutedEventArgs e)
        {
            if (Item.IsEditable && Item.IsInEditMode)
                Item.CommitEdit();

            if (Item is CmdContainer container)
            {
                // If any child change its state and no other item is selected; select this container
                if (container.SetIsSelectedOnChildren(false) && !ParentTreeView.SelectedTreeViewItems.Any())
                {
                    Item.IsSelected = true;
                }
                else
                {
                    // Give focus to any other selected item
                    ParentTreeView.SelectedTreeViewItems.FirstOrDefault()?.Focus();
                }
            }

            base.OnCollapsed(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e) => DragDrop.OnMouseDown(this, e);
        protected override void OnDragEnter(DragEventArgs e) => DragDrop.OnDragEnter(this, e);
        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e) => DragDrop.OnQueryContinueDrag(this, e);
        protected override void OnDragOver(DragEventArgs e) => DragDrop.OnDragOver(this, e);
        protected override void OnDragLeave(DragEventArgs e) => DragDrop.OnDragLeave(this, e);
        protected override void OnDrop(DragEventArgs e) => DragDrop.HandleDropForTarget(this, e);

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(LevelProperty), typeof(int), typeof(TreeViewItemEx), new PropertyMetadata(0));
    }

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
                    var dataObject = new DataObject();
                    dataObject.SetData(CmdArgsPackage.ClipboardCmdItemFormat, dragInfo.SourceItems);

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"StartDrag: {dragInfo.DirectVisualSourceItem.Item}");
                        dragInfo.IsDragInProgress = true;
                        var result = System.Windows.DragDrop.DoDragDrop(treeView, dataObject, DragDropEffects.Move);
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

            if (dropInfo.CanHadleDrop(e))
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

            if (dropInfo!= null && dropInfo.CanHadleDrop(e))
            {
                dropInfo.UpdateInsertPosition(e);
                dropInfo.UpdateTargetCollectionAndIndex();
                if (dragInfo.SourceItems.OfType<CmdContainer>().Any(container => Equals(container.Items, dropInfo.TargetCollection)))
                    e.Effects = DragDropEffects.None;
                else
                    e.Effects = DragDropEffects.Move;
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
            e.Handled = true;
            if (dragInfo?.IsDragInProgress == true)
                return;

            dropInfo.UpdateTargetCollectionAndIndex();

            HandleDropForTarget(e.Effects);
        }

        public static void HandleDropForTarget(DragDropEffects result)
        {
            System.Diagnostics.Debug.WriteLine($"HandleDropForTarget: {dropInfo.TargetItem.Item}");

            if (result.HasFlag(DragDropEffects.Move))
            {
                var idx = dropInfo.InsertIndex;
                foreach (var sourceItem in dragInfo.SourceItems)
                {
                    dropInfo.TargetCollection.Insert(idx++, sourceItem);
                }
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

            if (dropInfo != null)
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
                if (TargetItem.Item is CmdContainer)
                {
                    if (mousePosition.Y < itemHeight * 0.25 && !(TargetItem.Item is CmdProject))
                        InsertPosition = RelativInsertPosition.BeforeTargetItem;
                    else if (mousePosition.Y < itemHeight * 0.75)
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

        public bool CanHadleDrop(DragEventArgs e)
        {
            return e.Data.GetDataPresent(CmdArgsPackage.ClipboardCmdItemFormat);
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
