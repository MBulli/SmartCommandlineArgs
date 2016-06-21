using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Helper
{
    class NullToBoolConverter : ConverterBase
    {
        public NullToBoolConverter()
        {
        }

        public bool Inverted
        {
            get;
            set;
        }

        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // hehehe
            return Inverted != (value != null);
        }

        public override object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
