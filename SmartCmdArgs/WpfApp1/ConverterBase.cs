using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WpfApp1
{
    [System.Windows.Markup.MarkupExtensionReturnType(typeof(ConverterBase))]
    abstract class ConverterBase : System.Windows.Markup.MarkupExtension, IValueConverter
    {
        public ConverterBase()
        {
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public abstract object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
        public abstract object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
    }

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
