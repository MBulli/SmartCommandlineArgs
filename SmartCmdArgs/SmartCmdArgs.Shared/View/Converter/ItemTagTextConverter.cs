using SmartCmdArgs.ViewModel;
using System;
using System.Globalization;

namespace SmartCmdArgs.View.Converter
{
    class ItemTagTextConverter : ConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CmdParamType argType)
            {
                switch (argType)
                {
                    case CmdParamType.CmdArg: return "CLA";
                    case CmdParamType.EnvVar: return "ENV";
                    case CmdParamType.WorkDir: return "WD";
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
