using System;
using System.Globalization;

namespace SmartCmdArgs.View.Converter
{
    class FontSizeConverter : ConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // add input parameter testing as needed.
            var originalFontSize = (double)value;
            double alteredFontSize = originalFontSize * Ratio; ;

            return alteredFontSize;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        // Create a ratio property 
        // allows the converter to be used for different font ratios
        public double Ratio { get; set; }
    }
}
