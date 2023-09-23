using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace SmartCmdArgs.Helper
{
    public class EnumDescriptionTypeConverter : EnumConverter
    {
        private readonly Type _enumType;
        private readonly Dictionary<string, object> _enumValueCache = new Dictionary<string, object>();

        public EnumDescriptionTypeConverter(Type type) : base(type)
        {
            _enumType = type;
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                if (value != null)
                {
                    FieldInfo fi = value.GetType().GetField(value.ToString());
                    if (fi != null)
                    {
                        var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                        return ((attributes.Length > 0) && (!string.IsNullOrEmpty(attributes[0].Description)))
                            ? attributes[0].Description
                            : value.ToString();
                    }
                }
                return string.Empty;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
            {
                if (_enumValueCache.TryGetValue(stringValue, out var enumValue))
                    return enumValue;

                foreach (FieldInfo fi in _enumType.GetFields())
                {
                    var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                    if ((attributes.Length > 0) && (attributes[0].Description == stringValue))
                    {
                        _enumValueCache[stringValue] = fi.GetValue(fi.Name);
                        return fi.GetValue(fi.Name);
                    }
                }
                if (_enumValueCache.TryGetValue(stringValue, out enumValue))
                    return enumValue;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
