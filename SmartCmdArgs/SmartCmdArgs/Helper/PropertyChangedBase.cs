using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartCmdArgs.Helper
{
    public class PropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void SetAndNotify<T>(T newValue, ref T field, [CallerMemberName]string propertyName = null)
        {
            if (Equals(newValue, field)) return;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void OnNotifyPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public interface INotifyPropertyChangedDetailed
    {
        event PropertyChangedDetailedEventHandler PropertyChangedDetailed;
    }

    public class PropertyChangedDetailedBase : PropertyChangedBase, INotifyPropertyChangedDetailed
    {
        public event PropertyChangedDetailedEventHandler PropertyChangedDetailed;

        protected override void SetAndNotify<T>(T newValue, ref T field, [CallerMemberName]string propertyName = null)
        {
            if (Equals(newValue, field)) return;
            var oldValue = field;
            base.SetAndNotify(newValue, ref field, propertyName);
            PropertyChangedDetailed?.Invoke(this, new PropertyChangedDetailedEventArgs(propertyName, oldValue, newValue, typeof(T)));
        }
    }

    public delegate void PropertyChangedDetailedEventHandler(object sender, PropertyChangedDetailedEventArgs e);
    public class PropertyChangedDetailedEventArgs : PropertyChangedEventArgs
    {
        public object OldValue { get; }
        public object NewValue { get; }
        public Type PropertyType { get; }

        public PropertyChangedDetailedEventArgs(string propertyName, object oldValue, object newValue, Type propertyType) 
            : base(propertyName)
        {
            OldValue = oldValue;
            NewValue = newValue;
            PropertyType = propertyType;
        }
    }
}
