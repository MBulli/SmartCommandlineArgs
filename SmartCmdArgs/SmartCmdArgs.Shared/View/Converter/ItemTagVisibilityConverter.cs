using SmartCmdArgs.ViewModel;
using System;
using System.Globalization;
using System.Windows;

namespace SmartCmdArgs.View.Converter
{
    class ItemTagVisibilityConverter : MultiConverterBase
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var argType = values[0] as CmdParamType?;
            var showClaTag = values[1] as bool?;

            if (argType == CmdParamType.EnvVar || argType == CmdParamType.WorkDir || showClaTag == true)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
