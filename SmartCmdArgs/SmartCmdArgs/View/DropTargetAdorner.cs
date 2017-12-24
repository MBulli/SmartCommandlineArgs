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
        private static readonly PathGeometry triangle;
        private static readonly Pen pen;

        static DropTargetAdorner()
        {
            // Create the pen and triangle in a static constructor and freeze them to improve performance.
            const int triangleSize = 5;

            var firstLine = new LineSegment(new Point(0, -triangleSize), false);
            firstLine.Freeze();
            var secondLine = new LineSegment(new Point(0, triangleSize), false);
            secondLine.Freeze();

            var figure = new PathFigure { StartPoint = new Point(triangleSize, 0) };
            figure.Segments.Add(firstLine);
            figure.Segments.Add(secondLine);
            figure.Freeze();

            triangle = new PathGeometry();
            triangle.Figures.Add(figure);
            triangle.Freeze();

            pen = new Pen(Brushes.Gray, 2);
            pen.Freeze();
        }

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
                drawingContext.DrawRoundedRectangle(Brushes.Transparent, pen, new Rect(new Point(indent, 0), (Point)AdornedElement.RenderSize), 2, 2);
            }
            else if (dropInfo.InsertPosition != DropInfo.RelativInsertPosition.None)
            {
                double yPos = 0.0;
                if (dropInfo.InsertPosition.HasFlag(DropInfo.RelativInsertPosition.AfterTargetItem))
                {
                    if (dropInfo.InsertPosition.HasFlag(DropInfo.RelativInsertPosition.IntoTargetItem))
                    {
                        yPos = header.RenderSize.Height;
                        indent = AdornedElement.RenderSize.Width - ((TreeViewItemEx)tvItem.ItemContainerGenerator.ContainerFromIndex(0)).HeaderBorder.RenderSize.Width;
                    }
                    else
                    {
                        yPos = tvItem.RenderSize.Height;
                    }
                }

                var p1 = new Point(indent, yPos);
                var p2 = new Point(AdornedElement.RenderSize.Width, yPos);

                drawingContext.DrawLine(pen, p1, p2);
                DrawTriangle(drawingContext, p1, 0);
                DrawTriangle(drawingContext, p2, 180);
            }
        }
        
        private void DrawTriangle(DrawingContext drawingContext, Point origin, double rotation)
        {
            drawingContext.PushTransform(new TranslateTransform(origin.X, origin.Y));
            drawingContext.PushTransform(new RotateTransform(rotation));

            drawingContext.DrawGeometry(pen.Brush, null, triangle);

            drawingContext.Pop();
            drawingContext.Pop();
        }

        internal void Detach()
        {
            adornerLayer.Remove(this);
        }
    }
}
