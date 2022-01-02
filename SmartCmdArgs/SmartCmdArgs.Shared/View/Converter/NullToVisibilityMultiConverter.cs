using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.View.Converter
{
    class NullToVisibilityMultiConverter : MultiConverterBase
    {
        public enum VisibleCond { AllNotNull, AnyNotNull, AllNull, AnyNull };

        public VisibleCond VisibleCondition { get; set; } = VisibleCond.AllNotNull;

        public System.Windows.Visibility HideVisibility { get; set; } = System.Windows.Visibility.Hidden;

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;
            switch (VisibleCondition)
            {
                case VisibleCond.AllNotNull:
                    isVisible = values.All(x => x != null); break;
                case VisibleCond.AnyNotNull:
                    isVisible = values.Any(x => x != null); break;
                case VisibleCond.AllNull:
                    isVisible = values.All(x => x == null); break;
                case VisibleCond.AnyNull:
                    isVisible = values.Any(x => x == null); break;
            }

            return isVisible ? System.Windows.Visibility.Visible : HideVisibility;
        }

        public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
