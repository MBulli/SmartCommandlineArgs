using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.View.Converter
{
    class BooleanBoldFontConverter : ConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((value as bool?) == true)
                return System.Windows.FontWeights.Bold;
            else
                return System.Windows.FontWeights.Normal;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
