using Microsoft.VisualStudio.PlatformUI;
using SmartCmdArgs.Helper;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmartCmdArgs.View
{
    public class RedBorderAdorner : Adorner
    {
        private readonly Rectangle _border;

        public RedBorderAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _border = new Rectangle
            {
                Stroke = (Brush)FindResource(EnvironmentColors.VizSurfaceRedMediumBrushKey),
                StrokeThickness = 1.5,
                RadiusX = 2,
                RadiusY = 2,
                IsHitTestVisible = false
            };

            AddVisualChild(_border);

            AttachToTreeViewItems();
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _border;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _border.Arrange(new Rect(finalSize));
            return finalSize;
        }

        public void AttachToTreeViewItems()
        {
            var currentTreeViewItem = TreeHelper.FindAncestor<TreeViewItem>(AdornedElement);

            while (currentTreeViewItem != null)
            {
                currentTreeViewItem.Collapsed += OnTreeViewItemCollapsed;
                currentTreeViewItem.Expanded += OnTreeViewItemExpanded;

                currentTreeViewItem = TreeHelper.FindAncestor<TreeViewItem>(currentTreeViewItem);
            }
        }

        private void OnTreeViewItemCollapsed(object sender, RoutedEventArgs e)
        {
            _border.Visibility = Visibility.Collapsed;
        }

        private void OnTreeViewItemExpanded(object sender, RoutedEventArgs e)
        {
            _border.Visibility = Visibility.Visible;
        }
    }
}
