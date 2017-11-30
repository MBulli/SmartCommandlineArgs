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
using Microsoft.VisualStudio.Composition.Tasks;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    public class TreeViewEx : TreeView
    {
        static TreeViewEx()
        {
            RegisterCommand(ApplicationCommands.Copy, CopyCommandProperty);
            RegisterCommand(ApplicationCommands.Paste, PasteCommandProperty);
            RegisterCommand(ApplicationCommands.Cut, CutCommandProperty);

            CommandManager.RegisterClassCommandBinding(typeof(TreeViewEx), new CommandBinding(ApplicationCommands.SelectAll, 
                (sender, args) => ((TreeViewEx)sender).SelectAllItems(args), (sender, args) => args.CanExecute = ((TreeViewEx)sender).HasItems));

            void RegisterCommand(RoutedUICommand routedUiCommand, DependencyProperty commandProperty)
            {
                CommandManager.RegisterClassCommandBinding(typeof(TreeViewEx), 
                    new CommandBinding(
                        routedUiCommand, 
                        (sender, args) => ((ICommand)((DependencyObject)sender).GetValue(commandProperty))?.Execute(args.Parameter), 
                        (sender, args) => args.CanExecute = ((ICommand)((DependencyObject)sender).GetValue(commandProperty)).CanExecute(args.Parameter)));
            }
        }

        public static readonly DependencyProperty CopyCommandProperty = DependencyProperty.Register(
            nameof(CopyCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public static readonly DependencyProperty PasteCommandProperty = DependencyProperty.Register(
            nameof(PasteCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public static readonly DependencyProperty CutCommandProperty = DependencyProperty.Register(
            nameof(CutCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public ICommand CopyCommand { get => (ICommand)GetValue(CopyCommandProperty); set => SetValue(CopyCommandProperty, value); }
        public ICommand PasteCommand { get => (ICommand)GetValue(PasteCommandProperty); set => SetValue(PasteCommandProperty, value); }
        public ICommand CutCommand { get => (ICommand)GetValue(CutCommandProperty); set => SetValue(CutCommandProperty, value); }
        
        public static readonly DependencyProperty ToggleSelectedCommandProperty = DependencyProperty.Register(
            nameof(ToggleSelectedCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public ICommand ToggleSelectedCommand { get => (ICommand)GetValue(ToggleSelectedCommandProperty); set => SetValue(ToggleSelectedCommandProperty, value); }

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

            if (!GetIsItemSelected(item))
            {
                var aSelectedItem = SelectedTreeViewItems.FirstOrDefault();
                if (aSelectedItem != null)
                    aSelectedItem.Focus();
                else
                    SetIsItemSelected(item, true);
            }
        }

        private TreeViewItemEx _lastMouseDownTargetItem;
        public void MouseLeftButtonDownOnItem(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            _lastMouseDownTargetItem = tvItem;
            if (IsCtrlPressed || IsShiftPressed || !SelectedTreeViewItems.Skip(1).Any() || !GetIsItemSelected(tvItem))
            {
                SelectedItemChangedInternal(tvItem);
            }
        }

        public void MouseLeftButtonUpOnItem(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            if (IsCtrlPressed || IsShiftPressed || !SelectedTreeViewItems.Skip(1).Any())
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space && ToggleSelectedCommand?.CanExecute(null) == true)
            {
                ToggleSelectedCommand.Execute(null);
                e.Handled = true;
            }
            base.OnKeyDown(e);
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

        private void SelectAllItems(ExecutedRoutedEventArgs args)
        {
            foreach (var treeViewItem in GetTreeViewItems(this, false))
            {
                SetIsItemSelected(treeViewItem, true);
            }
            args.Handled = true;
        }
        
        protected override void OnMouseMove(MouseEventArgs e) => DragDrop.OnMouseMove(this, e);

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (HasItems)
                DragDrop.OnDragEnter((TreeViewItemEx)ItemContainerGenerator.ContainerFromIndex(Items.Count - 1), e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (HasItems)
                DragDrop.OnDragOver((TreeViewItemEx)ItemContainerGenerator.ContainerFromIndex(Items.Count - 1), e);
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            if (HasItems)
                DragDrop.OnDragLeave((TreeViewItemEx)ItemContainerGenerator.ContainerFromIndex(Items.Count - 1), e);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if (HasItems)
                DragDrop.HandleDropForTarget((TreeViewItemEx)ItemContainerGenerator.ContainerFromIndex(Items.Count - 1), e);
        }
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
            BindingOperations.ClearBinding(this, IsExpandedProperty);

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
                if (e.Key == Key.Return || e.Key == Key.F2)
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
                && ParentTreeView.SelectedTreeViewItems.Take(2).Count() == 1 
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

        protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is CmdBase item)
            {
                item.IsFocusedItem = (bool)e.NewValue;
            }
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
}
