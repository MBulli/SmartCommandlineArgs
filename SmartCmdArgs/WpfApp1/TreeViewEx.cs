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
    }

    public class TreeViewItemEx : TreeViewItem
    {
        private CmdBase Item => DataContext as CmdBase;

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

            if(Item?.IsEditable == true) Item.CommitEdit();
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (IsSelected && Item.IsEditable && !Item.IsInEditMode)
            {
                 Item.BeginEdit(initialValue: e.Text);
            }
        }


        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (IsSelected)
            {
                if (e.Key == Key.Space && !Item.IsInEditMode)
                {
                    Item.ToggleCheckedState();
                    e.Handled = true;
                }
                else if (e.Key == Key.Return || e.Key == Key.F2)
                {
                    if (Item.IsEditable && !Item.IsInEditMode)
                    {
                        Item.BeginEdit();
                        e.Handled = true;
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (IsSelected && Item.IsEditable && !Item.IsInEditMode)
            {
                Item.BeginEdit();
                e.Handled = true;
            }

            base.OnMouseLeftButtonDown(e);
        }



        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(LevelProperty), typeof(int), typeof(TreeViewItemEx), new PropertyMetadata(0));
    }
}
