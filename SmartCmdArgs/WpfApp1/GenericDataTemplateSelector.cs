using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace WpfApp1
{
    /// <summary>
    /// DataType cannot be null -> define Null type
    /// </summary>
    public class Null
    {
        
    }
    
    /// <summary>
    /// Generic template selector selecting the template according to the type
    /// https://stackoverflow.com/a/34144541/261645
    /// </summary>
    [ContentProperty("Items")]
    public class GenericDataTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Select a template according to the type of the object.
        /// </summary>
        /// <param name="item">The item for which a template shall be selected.</param>
        /// <param name="container">The container, in which the Template shall be used.</param>
        /// <returns>The appropriate template, if found; else null.</returns>
        public override DataTemplate SelectTemplate(object item, System.Windows.DependencyObject container)
        {
            DataTemplate defaultTemplate = null;
            if (item != null)
            {
                foreach (var dataTemplate in Items.OfType<DataTemplate>())
                {
                    if ((Type)dataTemplate.DataType == null || (Type)dataTemplate.DataType == typeof(Null))
                    {
                        defaultTemplate = dataTemplate;
                    }
                    else if (((Type)dataTemplate.DataType).IsInstanceOfType(item))
                    {
                        return dataTemplate;
                    }
                }
            }
            return defaultTemplate;
        }

        IList _items = new ArrayList();

        /// <summary>
        /// The list of templates.
        /// </summary>
        public IList Items
        {
            get => _items;
            set
            {
                Items.Clear();
                foreach (var item in value)
                {
                    if (item is DataTemplate)
                    {
                        _items.Add(item);
                    }
                }
            }
        }
    }

    public class TreeDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ProjectTemplate { get; set; }
        public DataTemplate GroupItemTemplate { get; set; }
        public DataTemplate ArgumentItemTemplate { get; set; }
        public DataTemplate EditArgumentTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            switch (item)
            {
                case CmdProject _: return ProjectTemplate;
                case CmdGroup _: return GroupItemTemplate;
                case CmdArgument _: return ArgumentItemTemplate;
                //case CmdArgument a when !a.IsSelected: return ArgumentItemTemplate;
                //case CmdArgument a when a.IsSelected: return EditArgumentTemplate;
                default: return null;
            }
        }
    }

    public class TreeItemStyleSelector : StyleSelector
    {
        public Style ProjectItemStyle { get; set; }
        public Style GroupItemStyle { get; set; }
        public Style ArgumentItemStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            switch (item)
            {
                case CmdProject _: return ProjectItemStyle;
                case CmdGroup _: return GroupItemStyle;
                case CmdArgument _: return ArgumentItemStyle;
                default: return null;
            }
        }
    }
}
