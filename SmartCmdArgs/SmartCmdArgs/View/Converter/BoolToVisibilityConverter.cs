using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SmartCmdArgs.View.Converter
{
    class BoolToVisibilityConverter : ConverterBase
    {
        public bool Inverted { get; set; } = false;

        public System.Windows.Visibility HideVisibility { get; set; } = System.Windows.Visibility.Hidden;

        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return HideVisibility;
            if (Inverted)
                return (value as bool?) == true ? HideVisibility : System.Windows.Visibility.Visible;
            else
                return (value as bool?) != true ? HideVisibility : System.Windows.Visibility.Visible;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
