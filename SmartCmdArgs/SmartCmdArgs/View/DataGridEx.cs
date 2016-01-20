using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using static SmartCmdArgs.Helper.DelayExecution;

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

        private DataGridCell GetDataGridCell(object item)
        {
            return (DataGridCell)Columns[1].GetCellContent(item)?.Parent;
        }

        private void ToggleEnabledForItem(object item)
        {
            if (ToggleItemEnabledCommand != null && ToggleItemEnabledCommand.CanExecute(item))
            {
                ToggleItemEnabledCommand.Execute(item);
            }
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
                    ExecuteAfter(TimeSpan.FromMilliseconds(10), () =>
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
                    ExecuteAfter(TimeSpan.FromMilliseconds(10), () =>
                        Keyboard.Focus(GetDataGridCell(focusedCellItem)));
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

            if (e.OriginalSource is ScrollViewer && SelectedItems.Count > 0)
                Keyboard.Focus(GetDataGridCell(SelectedItems[0]));
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


        public static readonly DependencyProperty MoveUpCommandProperty =
            DependencyProperty.Register("MoveUpCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

        public static readonly DependencyProperty MoveDownCommandProperty =
            DependencyProperty.Register("MoveDownCommand", typeof(ICommand), typeof(DataGridEx), new PropertyMetadata(null));

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
