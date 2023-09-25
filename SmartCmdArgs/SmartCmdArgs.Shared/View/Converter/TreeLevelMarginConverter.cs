﻿using System;

namespace SmartCmdArgs.View.Converter
{
    class TreeLevelMarginConverter : ConverterBase
    {
        public TreeLevelMarginConverter()
        {
        }

        public int Ident { get; set; } = 4;

        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int? treeLevel = value as int?;
            return new System.Windows.Thickness((treeLevel ?? 0) * Ident, 0, 0, 0);
        }

        public override object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
