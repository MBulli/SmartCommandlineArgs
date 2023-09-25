using SmartCmdArgs.ViewModel;
using System;
using System.Globalization;

namespace SmartCmdArgs.View.Converter
{
    class ItemTagTextConverter : ConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ArgumentType argType)
            {
                switch (argType)
                {
                    case ArgumentType.CmdArg: return "CLA";
                    case ArgumentType.EnvVar: return "ENV";
                    case ArgumentType.WorkDir: return "WD";
                }
            }

            return null;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
