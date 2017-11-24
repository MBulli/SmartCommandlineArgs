using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.View.Converter
{
    class EmptyToVisibilityConverter : ConverterBase
    {
        public EmptyToVisibilityConverter()
        {
        }

        public bool Inverted
        {
            get;
            set;
        }

        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool empty = false;
            if (value is ICollection collection && collection.Count == 0)
                empty = true;
            else if (value is IEnumerable enumerable && !enumerable.GetEnumerator().MoveNext())
                empty = true;

            if (Inverted)
                return !empty ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
            else
                return empty ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
