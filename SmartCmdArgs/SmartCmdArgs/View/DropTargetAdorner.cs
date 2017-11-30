using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SmartCmdArgs.View
{
    public class DropTargetAdorner : Adorner
    {
        private readonly AdornerLayer adornerLayer;
        private readonly DropInfo dropInfo;

        public DropTargetAdorner(UIElement adornedElement, DropInfo dropInfo) : base(adornedElement)
        {
            this.dropInfo = dropInfo;
            this.IsHitTestVisible = false;
            this.AllowDrop = false;
            this.SnapsToDevicePixels = true;
            adornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);
            adornerLayer.Add(this);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (dropInfo.Effects == DragDropEffects.None)
                return;

            var tvItem = dropInfo.TargetItem;
            var header = tvItem.HeaderBorder;
            var indent = AdornedElement.RenderSize.Width - header.RenderSize.Width;

            if (dropInfo.InsertPosition == DropInfo.RelativInsertPosition.IntoTargetItem)
            {
                drawingContext.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Red, 3), new Rect(new Point(indent, 0), (Point)AdornedElement.RenderSize));
            }
            else if (dropInfo.InsertPosition != DropInfo.RelativInsertPosition.None)
            {
                double yPos = 0.0;
                if (dropInfo.InsertPosition.HasFlag(DropInfo.RelativInsertPosition.AfterTargetItem))
                {
                    yPos = header.RenderSize.Height;
                    if (dropInfo.InsertPosition.HasFlag(DropInfo.RelativInsertPosition.IntoTargetItem))
                        indent = AdornedElement.RenderSize.Width - ((TreeViewItemEx)tvItem.ItemContainerGenerator.ContainerFromIndex(0)).HeaderBorder.RenderSize.Width;
                }
                drawingContext.DrawLine(new Pen(Brushes.Red, 3), new Point(indent, yPos), new Point(AdornedElement.RenderSize.Width, yPos));
            }
        }

        internal void Detach()
        {
            adornerLayer.Remove(this);
        }
    }
}
