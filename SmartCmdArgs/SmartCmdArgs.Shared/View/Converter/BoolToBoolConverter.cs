using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SmartCmdArgs.View.Converter
{
    class BoolToBoolConverter : ConverterBase
    {
        public bool NullValue { get; set; } = false;

        public bool Inverted { get; set; } = false;

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) {
                return Inverted != NullValue;
            }

            if (value is bool boolVal)
            {
                return Inverted != boolVal;
            }

            return null;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
