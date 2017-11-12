using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace WpfApp1
{
    public class TreeViewEx : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx();
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        //protected override void OnPreviewKeyDown(KeyEventArgs e)
        //{
        //    base.OnPreviewKeyDown(e);

        //    if (this.SelectedItem is IEditable)
        //    {
        //        if (e.Key == Key.Return || e.Key == Key.F2)
        //        {
        //            ((IEditable)this.SelectedItem).BeginEdit();
        //        }
        //        else if (e.Key >= Key.A && e.Key <= Key.Z)
        //        {
        //            ((IEditable)this.SelectedItem).BeginEdit(resetValue: true);
        //        }
        //    }
        //}
    }

    public class TreeViewItemEx : TreeViewItem
    {
        public int Level
        {
            get { return (int)GetValue(LevelProperty); }
            set { SetValue(LevelProperty, value); }
        }

        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx(this.Level+1);
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        public TreeViewItemEx(int level = 0)
        {
            Level = level;
        }

        protected override void OnUnselected(RoutedEventArgs e)
        {
            base.OnUnselected(e);

            Tree.FindVisualChild<ArgumentItemView>(this)?.CommitEdit();

            //var obj = DataContext as IEditable;
            //if (obj?.IsInEditMode == true)
            //{
            //    obj.CommitEdit();
            //}
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (IsSelected && Tree.FindVisualChild<ArgumentItemView>(this)?.IsInEditMode == false)
            {
                if (e.Key == Key.Return || e.Key == Key.F2)
                {
                    Tree.FindVisualChild<ArgumentItemView>(this)?.BeginEdit(resetValue: false);
                    
                    e.Handled = true;
                }
                else if (e.Key >= Key.A && e.Key <= Key.Z)
                {
                    Tree.FindVisualChild<ArgumentItemView>(this)?.BeginEdit(resetValue: true);
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (this.IsSelected)
            {
                Tree.FindVisualChild<ArgumentItemView>(this)?.BeginEdit(resetValue: false);
                e.Handled = true;

                //var obj = DataContext as IEditable;
                //if (obj != null && !obj.IsInEditMode)
                //{
                //    obj.BeginEdit();
                //}
            }

            base.OnMouseLeftButtonDown(e);
        }



        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(LevelProperty), typeof(int), typeof(TreeViewItemEx), new PropertyMetadata(0));
    }

    static class Tree
    {
        public static TChild FindVisualChild<TChild>(DependencyObject obj)
            where TChild : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is TChild)
                    return (TChild)child;
                else
                {
                    TChild childOfChild = FindVisualChild<TChild>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
    }
}
