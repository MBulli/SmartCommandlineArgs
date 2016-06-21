using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SmartCmdArgs.Helper
{
    [System.Windows.Markup.MarkupExtensionReturnType(typeof(MultiValueConverterBase))]
    abstract class MultiValueConverterBase : System.Windows.Markup.MarkupExtension, IMultiValueConverter
    {
        public MultiValueConverterBase()
        {
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public abstract object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture);
        public abstract object[] ConvertBack(object value, Type[] targetType, object parameter, System.Globalization.CultureInfo culture);
    }
}
