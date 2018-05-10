using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using SmartCmdArgs.Helper;
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
            RegisterCommand(ApplicationCommands.Delete, DeleteCommandProperty);

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
        public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
            nameof(DeleteCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public ICommand CopyCommand { get => (ICommand)GetValue(CopyCommandProperty); set => SetValue(CopyCommandProperty, value); }
        public ICommand PasteCommand { get => (ICommand)GetValue(PasteCommandProperty); set => SetValue(PasteCommandProperty, value); }
        public ICommand CutCommand { get => (ICommand)GetValue(CutCommandProperty); set => SetValue(CutCommandProperty, value); }
        public ICommand DeleteCommand { get => (ICommand)GetValue(DeleteCommandProperty); set => SetValue(DeleteCommandProperty, value); }

        public static readonly DependencyProperty ToggleSelectedCommandProperty = DependencyProperty.Register(
            nameof(ToggleSelectedCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public ICommand ToggleSelectedCommand { get => (ICommand)GetValue(ToggleSelectedCommandProperty); set => SetValue(ToggleSelectedCommandProperty, value); }

        public static readonly DependencyProperty SelectIndexCommandProperty = DependencyProperty.Register(
            nameof(SelectIndexCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public ICommand SelectIndexCommand { get => (ICommand)GetValue(SelectIndexCommandProperty); set => SetValue(SelectIndexCommandProperty, value); }

        public static readonly DependencyProperty SelectItemCommandProperty = DependencyProperty.Register(
            nameof(SelectItemCommand), typeof(ICommand), typeof(TreeViewEx), new PropertyMetadata(default(ICommand)));
        public ICommand SelectItemCommand { get => (ICommand)GetValue(SelectItemCommandProperty); set => SetValue(SelectItemCommandProperty, value); }

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

        public IEnumerable<TreeViewItemEx> VisibleTreeViewItems => GetTreeViewItems(this, false);

        public TreeViewEx()
        {
            DataContextChanged += OnDataContextChanged;            
        }

        private void OnDataContextChanged(object tv, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            SelectIndexCommand = new RelayCommand<int>(idx =>
            {
                var curIdx = 0;
                foreach (var treeViewItem in GetTreeViewItems(this, false))
                {
                    SetIsItemSelected(treeViewItem, false);
                    if (idx == curIdx)
                        treeViewItem.Focus();
                    curIdx++;
                }
            }, i => i >= 0);

            SelectItemCommand = new RelayCommand<object>(item =>
            {
                foreach (var treeViewItem in GetTreeViewItems(this, false))
                {
                    var curItem = treeViewItem.Item;
                    SetIsItemSelected(treeViewItem, false);
                    if (item == curItem)
                        treeViewItem.Focus();
                }
            }, o => o != null);
        }


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
                {
                    _lastItemSelected = aSelectedItem;
                    aSelectedItem.Focus();
                }
                else
                {
                    SetIsItemSelected(item, true);
                    _lastItemSelected = item;
                }
            }
        }

        public void MouseLeftButtonDownOnItem(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            if (IsCtrlPressed || IsShiftPressed || !SelectedTreeViewItems.Skip(1).Any() || !GetIsItemSelected(tvItem))
            {
                SelectedItemChangedInternal(tvItem);
            }
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
        private static IEnumerable<TreeViewItemEx> GetTreeViewItems(ItemsControl parentItem, bool includeCollapsedItems)
        {
            for (var index = 0; index < parentItem.Items.Count; index++)
            {
                var tvItem = parentItem.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItemEx;
                if (tvItem == null) continue;

                yield return tvItem;
                if (includeCollapsedItems || tvItem.IsExpanded)
                {
                    foreach (var item in GetTreeViewItems(tvItem, includeCollapsedItems))
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

        public void SelectItem(TreeViewItemEx item)
        {
            SelectedItemChangedInternal(item);
        }

        public void SelectItemExclusively(TreeViewItemEx item)
        {
            var items = GetTreeViewItems(this, includeCollapsedItems: true);
            foreach (var treeViewItem in items)
            {
                if (treeViewItem == item)
                {
                    if (!GetIsItemSelected(item))
                    {
                        SetIsItemSelected(treeViewItem, true);                        
                    }
                }
                else
                {
                    SetIsItemSelected(treeViewItem, false);
                }
            }
        }

        private void SelectAllItems(ExecutedRoutedEventArgs args)
        {
            foreach (var treeViewItem in GetTreeViewItems(this, false))
            {
                SetIsItemSelected(treeViewItem, true);
            }
            args.Handled = true;
        }

        public void ClearSelection()
        {
            var items = GetTreeViewItems(this, includeCollapsedItems: true);
            foreach (var treeViewItem in items)
            {
                SetIsItemSelected(treeViewItem, false);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            DragDrop.OnMouseMove(this, e);
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (HasItems)
            {
                var item = (TreeViewItemEx)ItemContainerGenerator.ContainerFromIndex(Items.Count - 1);
                DragDrop.OnDragEnter(item, e);
            }
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
        // Mouse state variables
        private bool justReceivedSelection = false;
        private CancellationTokenSource leftSingleClickCancelSource = null;
        private int leftMouseButtonClickCount = 0;

        public FrameworkElement HeaderBorder => GetTemplateChild("HeaderBorder") as FrameworkElement;

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
        
        public event KeyEventHandler HandledKeyDown
        {
            add => AddHandler(KeyDownEvent, value, true);
            remove => RemoveHandler(KeyDownEvent, value);
        }

        public TreeViewItemEx(TreeViewEx parentTreeView, int level = 0)
        {
            ParentTreeView = parentTreeView;
            Level = level;

            DataContextChanged += OnDataContextChanged;
            HandledKeyDown += OnHandledKeyDown;
            RequestBringIntoView += OnRequestBringIntoView;
        }

        private void OnHandledKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && !Item.IsInEditMode && Item.IsSelected)
            {
                var items = ParentTreeView.VisibleTreeViewItems.ToList();
                var indexToSelect = items.IndexOf(this);
                if (indexToSelect >= 0)
                {
                    indexToSelect = Math.Min(items.Count - 1, indexToSelect + 1);
                    ParentTreeView.SelectIndexCommand.Execute(indexToSelect);
                }
            }
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

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            Debug.WriteLine($"Entering OnMouseDown - ClickCount = {e.ClickCount}");
            e.Handled = true; // we handle clicks

            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right)
            {
                if (e.ClickCount == 1) // Single click
                {
                    bool wasSelected = Item.IsSelected;

                    // Let Tree select this item
                    ParentTreeView.MouseLeftButtonDownOnItem(this, e);
                    
                    // If the item was not selected before we change into pre-selection mode
                    // Aka. User clicked the item for the first time
                    if (!wasSelected && Item.IsSelected)
                    {
                        justReceivedSelection = true;
                    }
                }
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                leftMouseButtonClickCount = e.ClickCount;

                DragDrop.OnMouseDown(this, e);

                if (e.ClickCount > 1)
                {
                    // Cancel any single click action which was delayed
                    if (leftSingleClickCancelSource != null)
                    {
                        Debug.WriteLine("Cancel single click");
                        leftSingleClickCancelSource.Cancel();
                        leftSingleClickCancelSource = null;
                    }
                }
            }

            Debug.WriteLine("Leaving OnMouseDown");
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            // Note: e.ClickCount is always 1 for MouseUp
            Debug.WriteLine($"Entering OnMouseUp");          
            e.Handled = true; // we handle  clicks

            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right)
            {
                // If we just received the selection (inside MouseDown)
                if (!IsFocused && justReceivedSelection)
                {
                    Focus();
                }
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                // First click is special, as the only action to take is to select the item
                // Only do stuff if were not the first click
                if (!justReceivedSelection)
                {
                    bool hasManyItemsSelected = ParentTreeView.SelectedTreeViewItems.Take(2).Count() == 2;
                    bool shouldEnterEditMode = Item.IsEditable
                            && !Item.IsInEditMode
                            && !IsCtrlPressed;

                    // Only trigger actions if we're the first click in the DoubleClick timespan
                    if (leftMouseButtonClickCount == 1)
                    {
                        if (shouldEnterEditMode && !hasManyItemsSelected)
                        {
                            Debug.WriteLine("Triggered delayed enter edit mode");

                            // Wait for possible double click.
                            // Single click => edit; double click => toggle expand state
                            leftSingleClickCancelSource?.Cancel();
                            leftSingleClickCancelSource = new CancellationTokenSource();

                            var doubleClickTime = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime);
                            DelayExecution.ExecuteAfter(doubleClickTime, leftSingleClickCancelSource.Token, () =>
                            {
                                Debug.WriteLine("Delayed edit mode");
                                // Focus might have changed since first click
                                if (IsFocused)
                                {
                                    Item.BeginEdit();
                                }
                            });
                        }
                    }
                    else if(leftMouseButtonClickCount == 2)
                    {
                        if (!IsCtrlPressed && !IsShiftPressed)
                        {
                             // Remove selection of other items
                            ParentTreeView.SelectItemExclusively(this);

                            if (Item is CmdArgument)
                            {
                                if (shouldEnterEditMode)
                                {
                                    Item.BeginEdit();
                                    Debug.WriteLine("Enter edit mode with double click");
                                }
                            }

                            if (Item is CmdContainer)
                            {
                                IsExpanded = !IsExpanded;
                                Debug.WriteLine("Toggled expanded");
                            }                           
                        }
                    }
                }

                // Item is now officially selected
                justReceivedSelection = false;
                leftMouseButtonClickCount = 0;
            }

            Debug.WriteLine($"Leaving OnMouseUp");
        }

        protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                ParentTreeView.ChangedFocusedItem(this);
            }
        }

        protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is CmdBase item)
            {
                item.IsFocusedItem = (bool)e.NewValue;
            }
        }


        private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;

            var scrollView = ParentTreeView.Template.FindName("_tv_scrollviewer_", ParentTreeView) as ScrollViewer;
            var scrollPresenter = scrollView.Template.FindName("PART_ScrollContentPresenter", scrollView) as ScrollContentPresenter; // ScrollViewer without scrollbars

            // If item is not fully created, finish layout
            if (this.HeaderBorder == null)
            {
                UpdateLayout();
            }

            scrollPresenter?.MakeVisible(this, new Rect(new Point(0, 0), HeaderBorder.RenderSize));
        }
        

        protected override void OnDragEnter(DragEventArgs e) => DragDrop.OnDragEnter(this, e);
        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e) => DragDrop.OnQueryContinueDrag(this, e);
        protected override void OnDragOver(DragEventArgs e) => DragDrop.OnDragOver(this, e);
        protected override void OnDragLeave(DragEventArgs e) => DragDrop.OnDragLeave(this, e);
        protected override void OnDrop(DragEventArgs e) => DragDrop.HandleDropForTarget(this, e);

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(LevelProperty), typeof(int), typeof(TreeViewItemEx), new PropertyMetadata(0));
    }
}
