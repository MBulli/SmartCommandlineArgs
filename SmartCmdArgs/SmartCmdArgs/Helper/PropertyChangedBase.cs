using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartCmdArgs.Helper
{
    public class PropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetAndNotify<T>(T newValue, ref T field, [CallerMemberName]string propertyName = null)
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
}
