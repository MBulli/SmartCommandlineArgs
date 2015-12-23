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

        // Using a DependencyProperty as the backing store for MoveUpCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MoveUpCommandProperty =
            DependencyProperty.Register("MoveUpCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));


        public ICommand MoveDownCommand
        {
            get { return (ICommand)GetValue(MoveDownCommandProperty); }
            set { SetValue(MoveDownCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MoveUpCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MoveDownCommandProperty =
            DependencyProperty.Register("MoveDownCommand", typeof(ICommand), typeof(ListControl), new PropertyMetadata(null));


        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register("SelectedItems", typeof(IList), typeof(ListControl), new PropertyMetadata(null));


        public IList SelectedItems
        {
            get { return (IList)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }


        public ListControl()
        {
            InitializeComponent();

            this.CommandsDataGridProp.SelectedCellsChanged += DataGrid_SelectedCellsChanged;
        }

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            this.SelectedItems = this.CommandsDataGrid.SelectedItems;
        }

        private void DataGridCell_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            DataGridCell senderCell = ((DataGridCell)sender);
            bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (e.Key == Key.Space)
            {
                if (!senderCell.IsEditing)
                {
                    ToggleEnabledForSelectedCells(senderCell);
                    e.Handled = true;
                }
            }
            else if(e.Key == Key.Return)
            {
                if (!senderCell.IsEditing)
                {
                    // Enter edit mode if current cell is not in edit mode
                    senderCell.Focus();
                    this.CommandsDataGrid.BeginEdit();
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
            }
            else if (ctrlDown && e.Key == Key.Down)
            {
                if (MoveDownCommand != null && MoveDownCommand.CanExecute(null))
                {
                    MoveDownCommand.Execute(null);
                }
                e.Handled = true;
            }
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
                    ToggleEnabledForSelectedCells(senderCell);
                    // Keep current selection
                    e.Handled = true;
                }
            }
        }

        private void ToggleEnabledForSelectedCells(DataGridCell senderCell)
        {
            CmdArgItem senderItem = ((CmdArgItem)senderCell.DataContext);

            bool newState = !senderItem.Enabled;
            senderItem.Enabled = newState;

            foreach (var item in CommandsDataGrid.SelectedItems)
            {
                // This actually breaks MVVM
                ((CmdArgItem)item).Enabled = newState;
            }
        }

        // TODO: fix HACK
        public DataGrid CommandsDataGridProp => CommandsDataGrid;
    }
}
