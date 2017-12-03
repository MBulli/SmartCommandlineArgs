using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View.Converter
{
    class ItemMonikerConverter : MultiConverterBase
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var item = values[0];
            var isExpanded = (bool) values[1];
            if (item is CmdGroup grp)
            {
                if (isExpanded)
                    return KnownMonikers.FolderOpened;
                else
                    return KnownMonikers.FolderClosed;
            }
            if (item is CmdProject)
                return KnownMonikers.CSProjectNode;
            return null;
        }

        public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
