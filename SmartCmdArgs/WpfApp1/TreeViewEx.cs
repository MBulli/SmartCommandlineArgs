using System;
using System.Collections;
using System.Collections.Generic;
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

namespace WpfApp1
{
    public class TreeViewEx : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx(this);
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        // taken from https://stackoverflow.com/questions/459375/customizing-the-treeview-to-allow-multi-select
        #region Fields

        // Used in shift selections
        private TreeViewItem _lastItemSelected;

        #endregion Fields
        #region Dependency Properties

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

        #endregion Dependency Properties
        #region Properties

        private static bool IsCtrlPressed => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        private static bool IsShiftPressed => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        public IEnumerable<TreeViewItem> SelectedTreeViewItems => GetTreeViewItems(this, true).Where(GetIsItemSelected);
        public IList SelectedItems => SelectedTreeViewItems.Select(treeViewItem => treeViewItem.Header).ToList();

        #endregion Properties
        #region Event Handlers

        private TreeViewItem _lastMouseDownTargetItem;
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            // If clicking on a tree branch expander...
            if (e.OriginalSource is Shape || e.OriginalSource is Grid)
                return;

            var item = GetTreeViewItemClicked((FrameworkElement)e.OriginalSource);
            _lastMouseDownTargetItem = item;

            if (!IsCtrlPressed && !IsShiftPressed && SelectedItems.Count > 1 && GetIsItemSelected(item))
                return;

            if (item != null) SelectedItemChangedInternal(item);
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);

            // If clicking on a tree branch expander...
            if (e.OriginalSource is Shape)
                return;
            
            var item = GetTreeViewItemClicked((FrameworkElement)e.OriginalSource);

            if (item == null)
            {
                foreach (var treeViewItem in GetTreeViewItems(this, false))
                {
                    var cmdItem = (CmdBase)treeViewItem.Header;
                    if (cmdItem.IsInEditMode)
                        cmdItem.CommitEdit();
                }
                return;
            }

            if (IsCtrlPressed || IsShiftPressed || SelectedItems.Count <= 1)
                return;

            if (!Equals(item, _lastMouseDownTargetItem))
                return;

            SelectedItemChangedInternal(item);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.A && e.IsDown && IsCtrlPressed)
            {
                foreach (var treeViewItem in GetTreeViewItems(this, false))
                {
                    SetIsItemSelected(treeViewItem, true);
                }
                e.Handled = true;
            }
        }

        #endregion Event Handlers
        #region Utility Methods

        private void SelectedItemChangedInternal(TreeViewItem tvItem)
        {
            // Clear all previous selected item states if ctrl is NOT being held down
            if (!IsCtrlPressed)
            {
                var items = GetTreeViewItems(this, true);
                foreach (var treeViewItem in items)
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
        private static TreeViewItem GetTreeViewItemClicked(DependencyObject sender)
        {
            while (sender != null && !(sender is TreeViewItem))
                sender = VisualTreeHelper.GetParent(sender);
            return sender as TreeViewItem;
        }
        private static List<TreeViewItem> GetTreeViewItems(ItemsControl parentItem, bool includeCollapsedItems, List<TreeViewItem> itemList = null)
        {
            if (itemList == null)
                itemList = new List<TreeViewItem>();

            for (var index = 0; index < parentItem.Items.Count; index++)
            {
                var tvItem = parentItem.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
                if (tvItem == null) continue;

                itemList.Add(tvItem);
                if (includeCollapsedItems || tvItem.IsExpanded)
                    GetTreeViewItems(tvItem, includeCollapsedItems, itemList);
            }
            return itemList;
        }
        private List<TreeViewItem> GetTreeViewItemRange(TreeViewItem start, TreeViewItem end)
        {
            var items = GetTreeViewItems(this, false);

            var startIndex = items.IndexOf(start);
            var endIndex = items.IndexOf(end);
            var rangeStart = startIndex > endIndex || startIndex == -1 ? endIndex : startIndex;
            var rangeCount = startIndex > endIndex ? startIndex - endIndex + 1 : endIndex - startIndex + 1;

            if (startIndex == -1 && endIndex == -1)
                rangeCount = 0;

            else if (startIndex == -1 || endIndex == -1)
                rangeCount = 1;

            return rangeCount > 0 ? items.GetRange(rangeStart, rangeCount) : new List<TreeViewItem>();
        }

        public void ChangedFocusedItem(TreeViewItem item)
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

        #endregion Utility Methods
    }

    public class TreeViewItemEx : TreeViewItem
    {
        private CmdBase Item => DataContext as CmdBase;

        private static bool IsCtrlPressed => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
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

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindingOperations.ClearBinding(this, TreeViewEx.IsItemSelectedProperty);

            if (e.NewValue is CmdBase)
            {
                Binding bind = new Binding();
                bind.Source = e.NewValue;
                bind.Path = new PropertyPath(nameof(CmdBase.IsSelected));
                bind.Mode = BindingMode.TwoWay;
                SetBinding(TreeViewEx.IsItemSelectedProperty, bind);
            }
        }

        protected override void OnUnselected(RoutedEventArgs e)
        {
            base.OnUnselected(e);

            if(Item?.IsEditable == true) Item.CommitEdit();
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (IsSelected && Item.IsEditable && !Item.IsInEditMode)
            {
                 Item.BeginEdit(initialValue: e.Text);
            }
        }


        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (IsSelected)
            {
                var selectedItems = ParentTreeView.SelectedItems.Cast<CmdBase>().ToList();
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
            if (IsSelected && Item.IsEditable && !Item.IsInEditMode && !IsCtrlPressed && ParentTreeView.SelectedItems.Count == 1)
            {
                Item.BeginEdit();
                e.Handled = true;
            }

            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsKeyboardFocusedChanged(e);

            if ((bool) e.NewValue)
                ParentTreeView.ChangedFocusedItem(this);
        }

        protected override void OnCollapsed(RoutedEventArgs e)
        {
            base.OnCollapsed(e);

            var container = (CmdContainer)Item;

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

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(LevelProperty), typeof(int), typeof(TreeViewItemEx), new PropertyMetadata(0));
    }
}
