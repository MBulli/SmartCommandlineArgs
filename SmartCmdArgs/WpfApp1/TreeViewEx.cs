using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp1
{
    public class TreeViewEx : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx();
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (this.SelectedItem is IEditable)
            {
                if (e.Key == Key.Return || e.Key == Key.F2)
                {
                    ((IEditable)this.SelectedItem).BeginEdit();
                }
                else if(e.Key >= Key.A && e.Key <= Key.Z)
                {
                    ((IEditable)this.SelectedItem).BeginEdit(resetValue: true);
                }
            }
        }
    }

    public class TreeViewItemEx : TreeViewItem
    {
        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx();
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        protected override void OnUnselected(RoutedEventArgs e)
        {
            base.OnUnselected(e);

            var obj = DataContext as IEditable;
            if (obj?.IsInEditMode == true)
            {
                obj.EndEdit();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (this.IsSelected)
            {
                var obj = DataContext as IEditable;
                if (obj != null && !obj.IsInEditMode)
                {
                    obj.BeginEdit();
                }
            }

            base.OnMouseLeftButtonDown(e);
        }
    }
}
