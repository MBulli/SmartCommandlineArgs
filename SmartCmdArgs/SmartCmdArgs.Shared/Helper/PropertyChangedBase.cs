using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartCmdArgs.Helper
{
    public class PropertyChangedBase : INotifyPropertyChanged
    {
        private class NotificationStore : IDisposable
        {
            private readonly PropertyChangedBase obj;

            public NotificationStore(PropertyChangedBase obj)
            {
                this.obj = obj;

                _sotredNotifications = new HashSet<string>();
            }

            private readonly HashSet<string> _sotredNotifications;

            public void AddNotificationForProperty(string propName)
            {
                _sotredNotifications.Add(propName);
            }

            public void Dispose()
            {
                if (obj._notificationStore == this)
                {
                    obj._notificationStore = null;

                    foreach (var notification in _sotredNotifications)
                    {
                        obj.PropertyChanged?.Invoke(obj, new PropertyChangedEventArgs(notification));
                    }
                }
            }
        }

        private NotificationStore _notificationStore = null;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void SetAndNotify<T>(T newValue, ref T field, [CallerMemberName]string propertyName = null)
        {
            if (Equals(newValue, field)) return;
            field = newValue;

            if (_notificationStore == null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _notificationStore.AddNotificationForProperty(propertyName);
            }
        }

        public void OnNotifyPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected IDisposable PauseAndStoreNotifications()
        {
            if (_notificationStore != null)
            {
                _notificationStore = new NotificationStore(this);
                return _notificationStore;
            }
            else
            {
                return null;
            }
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
