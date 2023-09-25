using System.Windows;
using System.Windows.Controls;

using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    public class TreeItemStyleSelector : StyleSelector
    {
        public Style ProjectItemStyle { get; set; }
        public Style GroupItemStyle { get; set; }
        public Style ParameterItemStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            switch (item)
            {
                case CmdProject _: return ProjectItemStyle;
                case CmdGroup _: return GroupItemStyle;
                case CmdParameter _: return ParameterItemStyle;
                default: return null;
            }
        }
    }

    public class TreeDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ProjectTemplate { get; set; }
        public DataTemplate GroupItemTemplate { get; set; }
        public DataTemplate ParameterItemTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            switch (item)
            {
                case CmdProject _: return ProjectTemplate;
                case CmdGroup _: return GroupItemTemplate;
                case CmdParameter _: return ParameterItemTemplate;
                default: return null;
            }
        }
    }
}
