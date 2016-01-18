using SmartCmdArgs.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmartCmdArgs.View
{
    /// <summary>
    /// Interaction logic for CmdArgsListControl.xaml
    /// </summary>
    public partial class ListControl : UserControl
    {
        public ICommand MoveUpCommand
        {
            get { return (ICommand)GetValue(MoveUpCommandProperty); }
            set { SetValue(MoveUpCommandProperty, value); }
        }

        public ICommand MoveDownCommand
        {
            get { return (ICommand)GetValue(MoveDownCommandProperty); }
            set { SetValue(MoveDownCommandProperty, value); }
        }

        public ICommand ToggleItemEnabledCommand
        {
            get { return (ICommand)GetValue(ToggleItemEnabledProperty); }
            set { SetValue(ToggleItemEnabledProperty, value); }
        }      

        public IList SelectedItems
        {
            get { return (IList)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        public bool IsInEditMode
        {
            get { return (bool)GetValue(IsInEditModeProperty); }
            set { SetValue(IsInEditModeProperty, value); }
        }

        public ICommand CopyCommand
        {
            get { return (ICommand)GetValue(CopyCommandProperty); }
            set { SetValue(CopyCommandProperty, value); }
        }

        public ICommand PasteCommand
        {
            get { return (ICommand)GetValue(PasteCommandProperty); }
            set { SetValue(PasteCommandProperty, value); }
        }


        public ListControl()
        {
            InitializeComponent();

            this.CommandsDataGrid.SelectedCellsChanged += DataGrid_SelectedCellsChanged;
            this.CommandsDataGrid.PreviewKeyDown += CommandsDataGridPropOnPreviewKeyDown;
            this.CommandsDataGrid.BeginningEdit += CommandsDataGrid_BeginningEdit;
            this.CommandsDataGrid.CellEditEnding += CommandsDataGrid_CellEditEnding;
            this.CommandsDataGrid.MouseUp += CommandsDataGrid_MouseUp;
        }

        private void CommandsDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            this.IsInEditMode = true;
        }

        private void CommandsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            this.IsInEditMode = false;
        }

        private void CommandsDataGridPropOnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            DataGridCell senderCell = e.OriginalSource as DataGridCell;
            bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (e.Key == Key.Return)
            {
                if (senderCell != null && !senderCell.IsEditing)
                {
                    // Enter edit mode if current cell is not in edit mode
                    senderCell.Focus();
                    this.CommandsDataGrid.BeginEdit();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Space)
            {
                if (senderCell != null && !senderCell.IsEditing)
                {
                    object item = senderCell.DataContext;

                    // In some cases senderCell is not selected. This can happen after multi selection over all items.
                    if (!senderCell.IsSelected)
                    {
                        item = CommandsDataGrid.SelectedItem; // simply use first selected item
                    }

                    ToggleEnabledForItem(item);
                    e.Handled = true;
                }
            }
            else if (ctrlDown && e.Key == Key.Up)
            {
                if (MoveUpCommand != null && MoveUpCommand.CanExecute(null))
                {
                    var focusedCellItem = (Keyboard.FocusedElement as DataGridCell)?.DataContext;

                    MoveUpCommand.Execute(null);

                    // DataGrid loses keyboard focus after moving items
                    DelayExecution(TimeSpan.FromMilliseconds(10), () =>
                        Keyboard.Focus(GetDataGridCell(focusedCellItem)));
                }
                e.Handled = true;
            }
            else if (ctrlDown && e.Key == Key.Down)
            {
                if (MoveDownCommand != null && MoveDownCommand.CanExecute(null))
                {
                    var focusedCellItem = (Keyboard.FocusedElement as DataGridCell)?.DataContext;

                    MoveDownCommand.Execute(null);

                    // DataGrid loses keyboard focus after moving items
                    DelayExecution(TimeSpan.FromMilliseconds(10), () =>
                        Keyboard.Focus(GetDataGridCell(focusedCellItem)));
                }
                e.Handled = true;
            }
        }

        private void CommandsDataGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Commit edit if user clicks data grid background
            if (IsInEditMode && e.OriginalSource is ScrollViewer)
            {
                CommandsDataGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            }
        }

        private DataGridCell GetDataGridCell(object item)
        {
            return (DataGridCell)CommandsDataGrid.Columns[1].GetCellContent(item)?.Parent;
        }

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            this.SelectedItems = this.CommandsDataGrid.SelectedItems;
        }

        private void ToggleEnabledForItem(object item)
        {
            if (ToggleItemEnabledCommand != null && ToggleItemEnabledCommand.CanExecute(item))
            {
                ToggleItemEnabledCommand.Execute(item);
            }
        }

        private void DataGrid_OnExecuteCopy(object sender, ExecutedRoutedEventArgs e)
        {
            CopyCommand?.Execute(e.Parameter);
        }

        private void DataGrid_OnCanExecuteCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            var canExec = CopyCommand?.CanExecute(e.Parameter);
            e.CanExecute = canExec.GetValueOrDefault();
        }

        private void DataGrid_OnExecutePaste(object sender, ExecutedRoutedEventArgs e)
        {
            PasteCommand?.Execute(e.Parameter);
        }

        private void DataGrid_OnCanExecutePaste(object sender, CanExecuteRoutedEventArgs e)
        {
            var canExec = PasteCommand?.CanExecute(e.Parameter);
            e.CanExecute = canExec.GetValueOrDefault();
        }


        private static void DelayExecution(TimeSpan delay, Action action)
        {
            System.Threading.Timer timer = null;
            SynchronizationContext context = SynchronizationContext.Current;

            timer = new System.Threading.Timer(
                (ignore) =>
                {
                    timer.Dispose();

                    context.Post(ignore2 => action(), null);
                }, null, delay, TimeSpan.FromMilliseconds(-1));
        }

        public static readonly DependencyProperty MoveUpCommandProperty =
            DependencyProperty.Register("MoveUpCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty MoveDownCommandProperty =
            DependencyProperty.Register("MoveDownCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register("SelectedItems", typeof(IList), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ToggleItemEnabledProperty =
            DependencyProperty.Register("ToggleItemEnabledCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty IsInEditModeProperty =
            DependencyProperty.Register("IsInEditMode", typeof(bool), typeof(ListControl), new PropertyMetadata(false));

        public static readonly DependencyProperty CopyCommandProperty =
            DependencyProperty.Register("CopyCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty PasteCommandProperty =
            DependencyProperty.Register("PasteCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));
    }
}
