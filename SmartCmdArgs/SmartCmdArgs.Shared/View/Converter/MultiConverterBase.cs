using System;
using System.Globalization;
using System.Windows.Data;

namespace SmartCmdArgs.View.Converter
{
    [System.Windows.Markup.MarkupExtensionReturnType(typeof(MultiConverterBase))]
    abstract class MultiConverterBase : System.Windows.Markup.MarkupExtension, IMultiValueConverter
    {
        public MultiConverterBase()
        { }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public abstract object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
        public abstract object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
    }
}
