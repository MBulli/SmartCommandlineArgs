using System;
using System.Globalization;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View.Converter
{
    class ItemVisibilityConverter : ConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CmdArgument)
                return System.Windows.Visibility.Collapsed;
            return System.Windows.Visibility.Visible;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
