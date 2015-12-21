using SmartCmdArgs.ViewModel;
using System;
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
    public partial class CmdArgListControl : UserControl
    {
        // TODO add drag'n'drop to datagrid http://www.hardcodet.net/2009/03/moving-data-grid-rows-using-drag-and-drop

        public CmdArgListControl()
        {
            InitializeComponent();
        }

        private void DataGridCell_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            DataGridCell senderCell = ((DataGridCell)sender);

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
