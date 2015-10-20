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

        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;

            if (cell.Column is DataGridCheckBoxColumn)
            {
                // This actually breaks MVVM
                var item = (CmdArgItem)cell.DataContext;
                item.Enabled = !item.Enabled;
            }
        }

        // TODO: fix HACK
        public DataGrid CommandsDataGridProp => CommandsDataGrid;
    }
}
