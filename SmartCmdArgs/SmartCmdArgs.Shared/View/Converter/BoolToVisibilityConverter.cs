﻿using System;

namespace SmartCmdArgs.View.Converter
{
    class BoolToVisibilityConverter : ConverterBase
    {
        public bool Inverted { get; set; } = false;

        public System.Windows.Visibility HideVisibility { get; set; } = System.Windows.Visibility.Collapsed;

        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return HideVisibility;
            if (Inverted)
                return (value as bool?) == true ? HideVisibility : System.Windows.Visibility.Visible;
            else
                return (value as bool?) != true ? HideVisibility : System.Windows.Visibility.Visible;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
