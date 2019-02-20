using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using SmartCmdArgs.Helper;
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

            textbox.Visibility = Visibility.Collapsed;
            textblock.Visibility = Visibility.Visible;
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


        private int OldSelectionStart = -1;
        private int OldSelectionEnd = -1;

        private void textbox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Item.IsInEditMode)
            {
                Item.CommitEdit();
            }

            OldSelectionStart = -1;
            OldSelectionEnd = -1;
        }


        private static double ScrollOffset = 10;
        private void Textbox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            int index = -1;

            // check if Selection end or start have changed and the changed one as the index to scroll to.
            if (OldSelectionEnd != textbox.SelectionStart + textbox.SelectionLength)
                index = OldSelectionEnd = textbox.SelectionStart + textbox.SelectionLength;
            if (OldSelectionStart != textbox.SelectionStart)
                index = OldSelectionStart = textbox.SelectionStart;

            // if neither start or end have changed, keep scroll position
            if (index < 0)
                return;

            // on the first edit on a line the rect will be empty
            var rect = textbox.GetRectFromCharacterIndex(index);
            if (rect == Rect.Empty)
                return;

            // get point to scroll to relative to the TreeViewItem to include the indent
            var treeViewItem = TreeHelper.FindAncestorOrSelf<TreeViewItemEx>(this);
            var point = textbox.TranslatePoint(rect.TopLeft, treeViewItem);

            var sv = TreeHelper.FindAncestorOrSelf<ScrollViewer>(treeViewItem);

            // if the scroll offset to large, so the point we havt to make visible is left off screen we scroll left
            if (sv.HorizontalOffset > point.X - ScrollOffset)
            {
                sv.ScrollToHorizontalOffset(point.X - ScrollOffset);
            }
            // if the scroll offset to small, so the point we havt to make visible is right off screen we scroll right
            else if (sv.HorizontalOffset + sv.ActualWidth < point.X + ScrollOffset)
            {
                sv.ScrollToHorizontalOffset(point.X - sv.ActualWidth + ScrollOffset);
            }
        }
    }
}
