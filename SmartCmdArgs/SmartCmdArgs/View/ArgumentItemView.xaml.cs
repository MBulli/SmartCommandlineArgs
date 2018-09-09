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
using Microsoft.VisualStudio.Imaging;
using SmartCmdArgs.View.Converter;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    /// <summary>
    /// Interaction logic for ArgumentItemView.xaml
    /// </summary>
    public partial class ArgumentItemView : UserControl
    {
        private CmdBase Item => (CmdBase)DataContext;

        public ArgumentItemView()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindingOperations.ClearBinding(Icon, CrispImage.MonikerProperty);

            if (e.OldValue != null)
            {
                WeakEventManager<CmdBase, CmdBase.EditModeChangedEventArgs>.RemoveHandler((CmdBase)e.OldValue, nameof(CmdBase.EditModeChanged), OnItemEditModeChanged);
            }

            if (e.NewValue != null)
            {
                WeakEventManager<CmdBase, CmdBase.EditModeChangedEventArgs>.AddHandler((CmdBase)e.NewValue, nameof(CmdBase.EditModeChanged), OnItemEditModeChanged);

                if (e.NewValue is CmdContainer con)
                {
                    MultiBinding bind = new MultiBinding
                    {
                        Mode = BindingMode.OneWay,
                        Converter = new ItemMonikerConverter()
                    };
                    bind.Bindings.Add(new Binding { Source = con });
                    bind.Bindings.Add(new Binding
                    {
                        Source = con,
                        Path = new PropertyPath(nameof(CmdContainer.IsExpanded))
                    });
                    Icon.SetBinding(CrispImage.MonikerProperty, bind);
                }
            }
        }

        private void OnItemEditModeChanged(object sender, CmdBase.EditModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case CmdBase.EditMode.BeganEdit:
                    EnterEditMode(selectAll: true);
                    break;
                case CmdBase.EditMode.BeganEditAndReset:
                    EnterEditMode(selectAll: false);
                    break;
                case CmdBase.EditMode.CanceledEdit:
                    LeaveEditMode(editCanceled: true);
                    break;
                case CmdBase.EditMode.CommitedEdit:
                    LeaveEditMode(editCanceled: false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(e), e, null);
            }
        }

        private void EnterEditMode(bool selectAll)
        {
            textbox.Text = Item.Value;

            textblock.Visibility = Visibility.Collapsed;
            textbox.Visibility = Visibility.Visible;
            textbox.Focus();

            if (selectAll)
                textbox.SelectAll();
            else
                textbox.CaretIndex = textbox.Text.Length;
        }

        private void LeaveEditMode(bool editCanceled)
        {
            if (!editCanceled)
            {
                Item.Value = textbox.Text;
            }

            textblock.Visibility = Visibility.Visible;
            textbox.Visibility = Visibility.Hidden;
        }

        private void Textbox_OnKeyDown(object sender, KeyEventArgs e)
        {
            // Escape is a CmdKey and hence handled in ToolWindow

            if (e.Key == Key.Return && Item.IsInEditMode)
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
