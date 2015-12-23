using SmartCmdArgs.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        public ICommand ToogleSelectedItemsEnabledCommand
        {
            get { return (ICommand)GetValue(ToogleSelectedItemsEnabledCommandProperty); }
            set { SetValue(ToogleSelectedItemsEnabledCommandProperty, value); }
        }      

        public IList SelectedItems
        {
            get { return (IList)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }


        public ListControl()
        {
            InitializeComponent();

            this.CommandsDataGrid.SelectedCellsChanged += DataGrid_SelectedCellsChanged;
            this.CommandsDataGrid.PreviewKeyDown += CommandsDataGridPropOnPreviewKeyDown;
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
                    ToggleEnabledForSelectedCells();
                    e.Handled = true;
                }
            }
            else if (ctrlDown && e.Key == Key.Up)
            {
                if (MoveUpCommand != null && MoveUpCommand.CanExecute(null))
                {
                    MoveUpCommand.Execute(null);
                }
                e.Handled = true;
                CommandsDataGrid.Focus();           // DataGrid loses keyboard focus after moving items
            }
            else if (ctrlDown && e.Key == Key.Down)
            {
                if (MoveDownCommand != null && MoveDownCommand.CanExecute(null))
                {
                    MoveDownCommand.Execute(null);
                }
                e.Handled = true;
                CommandsDataGrid.Focus();           // DataGrid loses keyboard focus after moving items
            }
        }

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            this.SelectedItems = this.CommandsDataGrid.SelectedItems;
        }
        
        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridCell senderCell = ((DataGridCell)sender);
            CmdArgItem senderItem = ((CmdArgItem)senderCell.DataContext);

            if (senderCell.Column is DataGridCheckBoxColumn)
            {
                // Single row clicked which doesn't belong to a multi selection
                if (!senderCell.IsSelected)
                {
                    senderItem.Enabled = !senderItem.Enabled;
                }
                else
                {
                    // Selected row which possibly takes part in a multi selection
                    ToggleEnabledForSelectedCells();
                    // Keep current selection
                    e.Handled = true;
                }
            }
        }

        private void ToggleEnabledForSelectedCells()
        {
            if (ToogleSelectedItemsEnabledCommand != null && ToogleSelectedItemsEnabledCommand.CanExecute(null))
            {
                ToogleSelectedItemsEnabledCommand.Execute(null);
            }
        }


        public static readonly DependencyProperty MoveUpCommandProperty =
            DependencyProperty.Register("MoveUpCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty MoveDownCommandProperty =
            DependencyProperty.Register("MoveDownCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register("SelectedItems", typeof(IList), typeof(ListControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ToogleSelectedItemsEnabledCommandProperty =
            DependencyProperty.Register("ToogleSelectedItemsEnabledCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));
    }
}
