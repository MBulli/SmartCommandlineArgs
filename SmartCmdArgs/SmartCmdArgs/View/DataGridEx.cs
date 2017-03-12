using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SmartCmdArgs.Helper;
using static SmartCmdArgs.Helper.DelayExecution;
using static SmartCmdArgs.Helper.TreeHelper;

namespace SmartCmdArgs.View
{
    public class DataGridEx : DataGrid
    {
        public bool IsInEditMode
        {
            get { return (bool)GetValue(IsInEditModeProperty); }
            set { SetValue(IsInEditModeProperty, value); }
        }

        public IList SelectedItems
        {
            get { return (IList)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

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

        public ICommand CopyCommandlineCommand
        {
            get { return (ICommand)GetValue(CopyCommandlineCommandProperty); }
            set { SetValue(CopyCommandlineCommandProperty, value); }
        }

        public ICommand ToggleItemEnabledCommand
        {
            get { return (ICommand)GetValue(ToggleItemEnabledProperty); }
            set { SetValue(ToggleItemEnabledProperty, value); }
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

        public ICommand CutCommand
        {
            get { return (ICommand)GetValue(CutCommandProperty); }
            set { SetValue(CutCommandProperty, value); }
        }

        public DataGridEx()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnExecuteCopy, OnCanExecuteCopy));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, OnExecuteCut, OnCanExecuteCut));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnExecutePaste, OnCanExecutePaste));
        }

        private void FocusCellAfterDelay(object item, int columnIndex = 1, int delayInMs = 10)
        {
            ExecuteAfter(TimeSpan.FromMilliseconds(delayInMs), () => FocusCell(item));
        }

        private void FocusCell(object item, int columnIndex = 1)
        {
            DataGridRow row = ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            if (row == null)
            {
                ScrollIntoView(item);
                row = ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            }
            if (row != null)
            {
                DataGridCell cell = GetCell(row, columnIndex);
                cell?.Focus();
            }
        }

        private void SelectAndSetFocusToItem(object item)
        {
            if (IsInEditMode)
                CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            SelectedItems?.Clear();
            SelectedItem = item;
            ScrollIntoView(item);

            FocusCellAfterDelay(item);
        }

        private void ToggleEnabledForItem(object item)
        {
            if (ToggleItemEnabledCommand != null && ToggleItemEnabledCommand.CanExecute(item))
            {
                ToggleItemEnabledCommand.Execute(item);
            }
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            if (e.Action == NotifyCollectionChangedAction.Add)
                SelectAndSetFocusToItem(e.NewItems.Cast<object>().First());
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            DataGridCell senderCell = e.OriginalSource as DataGridCell;
            bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (e.Key == Key.Return)
            {
                if (senderCell != null && !senderCell.IsEditing)
                {
                    // Enter edit mode if current cell is not in edit mode
                    senderCell.Focus();
                    this.BeginEdit();
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
                        item = SelectedItem; // simply use first selected item
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
                    FocusCellAfterDelay(focusedCellItem);
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
                    FocusCellAfterDelay(focusedCellItem);
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            // Commit edit if user clicks data grid background
            if (IsInEditMode && e.OriginalSource is ScrollViewer)
            {
                CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            }
        }

        public void CheckboxCellOnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInEditMode)
                CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var checkBoxCell = (DataGridCell)sender;
            FocusCell(new DataGridCellInfo(checkBoxCell).Item);
        }

        protected override void OnSelectedCellsChanged(SelectedCellsChangedEventArgs e)
        {
            base.OnSelectedCellsChanged(e);

            this.SelectedItems = base.SelectedItems;
        }

        protected override void OnBeginningEdit(DataGridBeginningEditEventArgs e)
        {
            this.IsInEditMode = true;
        }

        protected override void OnCellEditEnding(DataGridCellEditEndingEventArgs e)
        {
            this.IsInEditMode = false;
        }


        private void OnExecuteCopy(object sender, ExecutedRoutedEventArgs e)
        {
            CopyCommand?.Execute(e.Parameter);
        }

        private void OnCanExecuteCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            var canExec = CopyCommand?.CanExecute(e.Parameter);
            e.CanExecute = canExec.GetValueOrDefault();
        }

        private void OnExecutePaste(object sender, ExecutedRoutedEventArgs e)
        {
            PasteCommand?.Execute(e.Parameter);
        }

        private void OnCanExecutePaste(object sender, CanExecuteRoutedEventArgs e)
        {
            var canExec = PasteCommand?.CanExecute(e.Parameter);
            e.CanExecute = canExec.GetValueOrDefault();
        }

        private void OnExecuteCut(object sender, ExecutedRoutedEventArgs e)
        {
            CutCommand?.Execute(e.Parameter);
        }

        private void OnCanExecuteCut(object sender, CanExecuteRoutedEventArgs e)
        {
            var canExec = CutCommand?.CanExecute(e.Parameter);
            e.CanExecute = canExec.GetValueOrDefault();
        }

        private DataGridCell GetCell(DataGridRow rowContainer, int column)
        {
            if (rowContainer != null)
            {
                DataGridCellsPresenter presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
                if (presenter == null)
                {
                    /* if the row has been virtualized away, call its ApplyTemplate() method 
                     * to build its visual tree in order for the DataGridCellsPresenter
                     * and the DataGridCells to be created */
                    rowContainer.ApplyTemplate();
                    presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
                }
                if (presenter != null)
                {
                    DataGridCell cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
                    if (cell == null)
                    {
                        /* bring the column into view
                         * in case it has been virtualized away */
                        ScrollIntoView(rowContainer, Columns[column]);
                        cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
                    }
                    return cell;
                }
            }
            return null;
        }


        public static readonly DependencyProperty MoveUpCommandProperty =
            DependencyProperty.Register("MoveUpCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty MoveDownCommandProperty =
            DependencyProperty.Register("MoveDownCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty CopyCommandlineCommandProperty =
            DependencyProperty.Register("CopyCommandlineCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register("SelectedItems", typeof(IList), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty ToggleItemEnabledProperty =
            DependencyProperty.Register("ToggleItemEnabledCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty IsInEditModeProperty =
            DependencyProperty.Register("IsInEditMode", typeof(bool), typeof(DataGridEx), new PropertyMetadata(false));

        public static readonly DependencyProperty CopyCommandProperty =
            DependencyProperty.Register("CopyCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty PasteCommandProperty =
            DependencyProperty.Register("PasteCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty CutCommandProperty =
            DependencyProperty.Register("CutCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

    }
}
