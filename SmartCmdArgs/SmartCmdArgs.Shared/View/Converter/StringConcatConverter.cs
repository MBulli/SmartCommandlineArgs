using System;
using System.Globalization;
using System.Linq;

namespace SmartCmdArgs.View.Converter
{
    class StringConcatConverter : MultiConverterBase
    {
        public string Seperator { get; set; } = ", ";

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Join(Seperator, values.OfType<string>());
        }

        public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
