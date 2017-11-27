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
    class DropTargetAdorner : Adorner
    {
        private readonly AdornerLayer adornerLayer;

        public DropTargetAdorner(UIElement adornedElement) : base(adornedElement)
        {
            this.IsHitTestVisible = false;
            this.AllowDrop = false;
            this.SnapsToDevicePixels = true;
            adornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);
            adornerLayer.Add(this);
        }

        public Point MousePosition { get; set; }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var tvItem = ((TreeViewItemEx)AdornedElement);
            var header = tvItem.GetHeaderBorder();

            var itemHeight = tvItem.RenderSize.Height;

            double yPos = 0;
            if (MousePosition.Y >= itemHeight / 2)
                yPos = itemHeight;

            drawingContext.DrawLine(new Pen(Brushes.Red, 5), new Point(AdornedElement.RenderSize.Width - header.RenderSize.Width , yPos), new Point(AdornedElement.RenderSize.Width, yPos));
        }

        internal void Detach()
        {
            adornerLayer.Remove(this);
        }
    }
}
