using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SmartCmdArgs.View.Converter
{
    class BoolToBoolMultiConverter : MultiConverterBase
    {
        public bool NullValue { get; set; } = false;

        public enum TrueCond { AllTrue, AnyTrue, AllFalse, AnyFalse };

        public TrueCond TrueCondition { get; set; } = TrueCond.AllTrue;

        public Visibility HideVisibility { get; set; } = Visibility.Hidden;

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            switch (TrueCondition)
            {
                case TrueCond.AllTrue:
                    return values.All(x => (bool?)x ?? NullValue);
                case TrueCond.AnyTrue:
                    return values.Any(x => (bool?)x ?? NullValue);
                case TrueCond.AllFalse:
                    return values.All(x => !((bool?)x ?? NullValue));
                case TrueCond.AnyFalse:
                    return values.Any(x => !((bool?)x ?? NullValue));
                default:
                    return false;
            }
        }

        public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
