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
    class BoolToVisibilityMultiConverter : MultiConverterBase
    {
        public bool NullValue { get; set; } = false;

        public enum VisibleCond { AllTrue, AnyTrue, AllFalse, AnyFalse };

        public VisibleCond VisibleCondition { get; set; } = VisibleCond.AllTrue;

        public Visibility HideVisibility { get; set; } = Visibility.Collapsed;

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;
            switch (VisibleCondition)
            {
                case VisibleCond.AllTrue:
                    isVisible = values.All(x => (bool?)x ?? NullValue); break;
                case VisibleCond.AnyTrue:
                    isVisible = values.Any(x => (bool?)x ?? NullValue); break;
                case VisibleCond.AllFalse:
                    isVisible = values.All(x => !((bool?)x ?? NullValue)); break;
                case VisibleCond.AnyFalse:
                    isVisible = values.Any(x => !((bool?)x ?? NullValue)); break;
            }

            return isVisible ? Visibility.Visible : HideVisibility;
        }

        public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
