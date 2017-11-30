using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
using EnvDTE80;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    /// <summary>
    /// Interaction logic for ArgumentItemView.xaml
    /// </summary>
    public partial class ArgumentItemView : UserControl
    {
        private CmdBase Item => (CmdBase) DataContext;

        public ArgumentItemView()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null)
                ((CmdBase)e.OldValue).EditModeChanged -= OnItemEditModeChanged;

            if (e.NewValue != null)
                ((CmdBase)e.NewValue).EditModeChanged += OnItemEditModeChanged;
        }

        private void OnItemEditModeChanged(object sender, CmdBase.EditMode e)
        {
            switch (e)
            {
                case CmdBase.EditMode.BeganEdit:
                    EnterEditMode(selectAll: true);
                    break;
                case CmdBase.EditMode.BeganEditAndReset:
                    EnterEditMode(selectAll: false);
                    break;
                case CmdBase.EditMode.CanceledEdit:
                case CmdBase.EditMode.CommitedEdit:
                    LeaveEditMode();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(e), e, null);
            }
        }

        private void EnterEditMode(bool selectAll)
        {
            textblock.Visibility = Visibility.Collapsed;
            textbox.Visibility = Visibility.Visible;
            textbox.Focus();

            if (selectAll)
                textbox.SelectAll();
            else
                textbox.CaretIndex = textbox.Text.Length;
        }

        private void LeaveEditMode()
        {
            textblock.Visibility = Visibility.Visible;
            textbox.Visibility = Visibility.Collapsed;
        }
        
        private void Textbox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && Item.IsInEditMode)
            {
                Item.CancelEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Return && Item.IsInEditMode)
            {
                Item.CommitEdit();
                e.Handled = true;
            }
        }

        private void textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Item.IsInEditMode)
            {
                Item.CommitEdit();
            }
        }
    }
}
