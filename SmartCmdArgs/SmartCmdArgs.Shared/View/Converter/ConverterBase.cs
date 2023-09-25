using System;
using System.Windows.Data;

namespace SmartCmdArgs.View.Converter
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
}
