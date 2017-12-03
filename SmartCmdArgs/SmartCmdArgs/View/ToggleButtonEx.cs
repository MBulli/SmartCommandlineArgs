using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SmartCmdArgs.View
{
    public class ToggleButtonEx : ToggleButton
    {
        public static readonly DependencyProperty ExpandedStrokeProperty = DependencyProperty.Register(
            nameof(ExpandedStroke), typeof(Brush), typeof(ToggleButtonEx), new PropertyMetadata(default(Brush)));

        public Brush ExpandedStroke
        {
            get => (Brush) GetValue(ExpandedStrokeProperty);
            set => SetValue(ExpandedStrokeProperty, value);
        }

        public static readonly DependencyProperty CollapsedFillProperty = DependencyProperty.Register(
            nameof(CollapsedFill), typeof(Brush), typeof(ToggleButtonEx), new PropertyMetadata(default(Brush)));

        public Brush CollapsedFill
        {
            get => (Brush) GetValue(CollapsedFillProperty);
            set => SetValue(CollapsedFillProperty, value);
        }

        public static readonly DependencyProperty CollapsedStrokeProperty = DependencyProperty.Register(
            nameof(CollapsedStroke), typeof(Brush), typeof(ToggleButtonEx), new PropertyMetadata(default(Brush)));

        public Brush CollapsedStroke
        {
            get => (Brush) GetValue(CollapsedStrokeProperty);
            set => SetValue(CollapsedStrokeProperty, value);
        }

        public ToggleButtonEx()
        { }
    }
}
