using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class CmdBase : PropertyChangedBase
    {
        private CmdContainer parent;
        public CmdContainer Parent { get => parent; set => SetAndNotify(value, ref this.parent); }

        private string value;
        public string Value { get => value; set => SetAndNotify(value, ref this.value); }

        protected bool? isChecked;
        public bool? IsChecked { get => isChecked; set => OnIsCheckedChanged(isChecked, value, true); }

        public virtual bool IsEditable => false;

        public CmdBase(string value, bool? isChecked)
        {
            this.value = value;
            this.isChecked = isChecked;
        }

        public void ToggleCheckedState()
        {
            if (IsChecked == null)
                IsChecked = false;
            else
                IsChecked = !IsChecked;
        }

        protected virtual void OnIsCheckedChanged(bool? oldValue, bool? newValue, bool notifyParent)
        {
            SetAndNotify(newValue, ref this.isChecked, nameof(IsChecked));

            if (notifyParent)
            {
                parent?.OnChildIsCheckedChanged(oldValue, newValue);
            }           
        }

        protected virtual void OnChildIsCheckedChanged(bool? oldValue, bool? newValue)
        {}

        private string editBackupValue;

        private bool isInEditMode;
        public bool IsInEditMode { get => isInEditMode; private set => SetAndNotify(value, ref this.isInEditMode); }

        public event EventHandler<EditMode> EditModeChanged;
        public enum EditMode
        {
            BeganEdit, BeganEditAndReset, CanceledEdit, CommitedEdit
        }

        public void BeginEdit(string initialValue = null)
        {
            ThrowIfNotEditable();

            if (!IsInEditMode)
            {
                editBackupValue = Value;
                if (initialValue != null) Value = initialValue;
                IsInEditMode = true;
                EditModeChanged?.Invoke(this, initialValue != null ? EditMode.BeganEditAndReset : EditMode.BeganEdit);
            }
        }

        public void CancelEdit()
        {
            ThrowIfNotEditable();

            if (IsInEditMode)
            {
                Value = editBackupValue;
                editBackupValue = null;
                IsInEditMode = false;
                EditModeChanged?.Invoke(this, EditMode.CanceledEdit);
            }
        }

        public void CommitEdit()
        {
            ThrowIfNotEditable();

            if (IsInEditMode)
            {
                editBackupValue = null;
                IsInEditMode = false;
                EditModeChanged?.Invoke(this, EditMode.CommitedEdit);
            }
        }

        private void ThrowIfNotEditable()
        {
            if (!IsEditable)
                throw new InvalidOperationException("Can't execute edit operation on a not editable item!");
        }
    }

    public interface ICmdItem
    {
        bool? IsChecked { get; set; }
        CmdContainer Parent { get; set; }

        void SetIsCheckedWithoutNotifyingParent(bool? value);
    }

    public class CmdContainer : CmdBase
    {
        public ObservableCollection<ICmdItem> Items { get; }

        public CmdContainer(string value, bool? isChecked, IEnumerable<ICmdItem> items = null) : base(value, isChecked)
        {
            Items = new ObservableCollection<ICmdItem>();

            Items.CollectionChanged += ItemsOnCollectionChanged;

            foreach (var item in items ?? Enumerable.Empty<ICmdItem>())
            {
                Items.Add(item);
            }
        }

        private void ItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            switch (notifyCollectionChangedEventArgs.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in notifyCollectionChangedEventArgs.NewItems.Cast<ICmdItem>())
                    {
                        item.Parent = this;
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (var item in notifyCollectionChangedEventArgs.OldItems.Cast<ICmdItem>())
                    {
                        item.Parent = null;
                    }
                    foreach (var item in notifyCollectionChangedEventArgs.NewItems.Cast<ICmdItem>())
                    {
                        item.Parent = this;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in notifyCollectionChangedEventArgs.OldItems.Cast<ICmdItem>())
                    {
                        item.Parent = null;
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (notifyCollectionChangedEventArgs.Action != NotifyCollectionChangedAction.Move)
            {
                if (Items.All(item => item.IsChecked ?? false))
                    base.OnIsCheckedChanged(IsChecked, true, true);
                else if (Items.All(item => !item.IsChecked ?? false))
                    base.OnIsCheckedChanged(IsChecked, false, true);
                else
                    base.OnIsCheckedChanged(IsChecked, null, true);
            }

        }

        protected override void OnIsCheckedChanged(bool? oldValue, bool? newValue, bool notifyParent)
        {
            base.OnIsCheckedChanged(oldValue, newValue, notifyParent);
            foreach (var item in Items)
            {
                item.SetIsCheckedWithoutNotifyingParent(newValue);
            }
        }

        protected override void OnChildIsCheckedChanged(bool? oldValue, bool? newValue)
        {
            if (newValue == true)
            {
                if (Items.All(item => item.IsChecked ?? false))
                    base.OnIsCheckedChanged(IsChecked, true, true);
                else
                    base.OnIsCheckedChanged(IsChecked, null, true);
            }
            else
            {
                if (Items.Any(item => item.IsChecked ?? true))
                    base.OnIsCheckedChanged(IsChecked, null, true);
                else
                    base.OnIsCheckedChanged(IsChecked, false, true);
            }
        }

    }

    public class CmdProject : CmdContainer
    {
        public CmdProject(string value, bool? isChecked = false, IEnumerable<ICmdItem> items = null) : base(value, isChecked, items)
        {
        }
    }

    public class CmdGroup : CmdContainer, ICmdItem
    {
        public override bool IsEditable => true;

        public CmdGroup(string value, bool? isChecked = false, IEnumerable<ICmdItem> items = null) : base(value, isChecked, items)
        {
        }
        
        public void SetIsCheckedWithoutNotifyingParent(bool? value)
        {
            OnIsCheckedChanged(IsChecked, value, false);
        }
    }

    public class CmdArgument : CmdBase, ICmdItem
    {
        public override bool IsEditable => true;

        public CmdArgument(string value, bool? isChecked = false) : base(value, isChecked)
        {
        }

        public void SetIsCheckedWithoutNotifyingParent(bool? value)
        {
            OnIsCheckedChanged(IsChecked, value, false);
        }
    }


    public class PropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetAndNotify<T>(T newValue, ref T field, [CallerMemberName]string propertyName = null)
        {
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void OnNotifyPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
