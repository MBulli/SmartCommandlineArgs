using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using SmartCmdArgs.View;

namespace SmartCmdArgs.Helper
{
    class FilterListByStringConverter : MultiValueConverterBase
    {
        public FilterListByStringConverter()
        {
        }

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return null;

            var list = values[0] as IList;
            var filter = values[1] as string;
            if (list == null || filter == null)
                return null;

            filter = filter.ToLower();
            return list.Cast<string>().Where(item => item.ToLower().Contains(filter)).OrderBy(item => item.ToLower().IndexOf(filter)).ToList();
        }

        public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
