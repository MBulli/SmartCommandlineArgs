using System;
using System.Linq;
using System.Windows.Input;
using SmartCmdArgs.Helper;

namespace SmartCmdArgs.ViewModel
{
    public class CmdBase : PropertyChangedBase
    {
        public Guid Id { get; }

        private CmdContainer parent;
        public CmdContainer Parent { get => parent; set => OnParentChanged(parent, value); }

        private string value;
        public string Value
        {
            get => value;
            set
            {
                if (value == this.@value) return;
                OnValueChanged(this.@value, value);
                NotifyPropertyChanged(nameof(DisplayText));
            }
        }

        public virtual string DisplayText { get => value; }

        protected bool? isChecked;
        public bool? IsChecked
        {
            get => isChecked;
            set
            {
                BubbleEvent(new CheckStateWillChangeEvent(this));
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || InExclusiveModeContainer)
                    ExclusiveChecked();
                else
                    OnIsCheckedChanged(isChecked, value, true);
            }
        }

        protected bool isSelected;
        public virtual bool IsSelected
        {
            get => isSelected;
            set { OnIsSelectedChanged(isSelected, value); }
        }
        
        public bool IsFocusedItem { get; set; }

        public virtual bool IsEditable => false;

        public bool IsActive => true;

        private string _projectConfig = null;
        public string ProjectConfig { get => _projectConfig; protected set => OnProjectConfigChanged(this._projectConfig, value); }
        public string UsedProjectConfig => _projectConfig ?? Parent?.UsedProjectConfig;

        private string _projectPlatform = null;
        public string ProjectPlatform { get => _projectPlatform; protected set => OnProjectPlatformChanged(this._projectPlatform, value); }
        public string UsedProjectPlatform => _projectPlatform ?? Parent?.UsedProjectPlatform;

        private string _launchProfile = null;
        public string LaunchProfile { get => _launchProfile; protected set => OnLaunchProfileChanged(this._launchProfile, value); }
        public string UsedLaunchProfile => _launchProfile ?? Parent?.UsedLaunchProfile;

        public bool InExclusiveModeContainer => Parent?.ExclusiveMode ?? false;

        public virtual Guid ProjectGuid => Parent?.ProjectGuid ?? Guid.Empty;

        public CmdBase(Guid id, string value, bool? isChecked = false)
        {
            if (id == Guid.Empty)
                id = Guid.NewGuid();
            Id = id;
            this.value = value;
            this.isChecked = isChecked;
        }

        /// <summary>
        /// Notifies the parent that an event occurred.
        /// The event will eventually reach the CmdProject root element
        /// </summary>
        protected virtual void BubbleEvent(TreeEventBase treeEvent, CmdBase receiver = null)
        {
            if (receiver != null)
            {
                receiver.BubbleEvent(treeEvent);
            }
            else
            {
                this.Parent?.BubbleEvent(treeEvent);
            }
        }

        protected virtual void OnParentChanged(CmdContainer oldParent, CmdContainer newParent)
        {
            SetAndNotify(newParent, ref this.parent, nameof(Parent));

            if (oldParent != newParent)
            {
                // Use oldParent if newParent is null as we've been removed from the tree
                // but still want to notify everbody that we were removed.
                var receiver = newParent ?? oldParent;
                BubbleEvent(new ParentChangedEvent(this, oldParent, newParent), receiver);
            }
        }

        protected virtual void OnValueChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref this.value, nameof(Value));

            if (oldValue != newValue)
            { 
                BubbleEvent(new ValueChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnIsSelectedChanged(bool oldValue, bool newValue)
        {
            SetAndNotify(newValue, ref isSelected, nameof(IsSelected));

            if (oldValue != newValue)
            {
                BubbleEvent(new SelectionChangedEvent(this, oldValue, newValue));
            }           
        }

        protected virtual void OnIsCheckedChanged(bool? oldValue, bool? newValue, bool notifyParent)
        {
            SetAndNotify(newValue, ref this.isChecked, nameof(IsChecked));

            if (notifyParent)
            {
                parent?.OnChildIsCheckedChanged(oldValue, newValue);
            }
            
            if (InExclusiveModeContainer && IsChecked != false)
            {
                var checkedItems = Parent.Items.Where(item => item.IsChecked != false);
                foreach (var item in checkedItems)
                {
                    if (item != this)
                    {
                        item.OnIsCheckedChanged(item.isChecked, false, true);
                    }
                }
            }

            // TODO: Only bubble event if we're the origin.
            if (oldValue != newValue)
            {
                BubbleEvent(new CheckStateChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnProjectConfigChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref _projectConfig, nameof(ProjectConfig));

            if (oldValue != newValue)
            {
                BubbleEvent(new ProjectConfigChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnProjectPlatformChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref _projectPlatform, nameof(ProjectPlatform));

            if (oldValue != newValue)
            {
                BubbleEvent(new ProjectPlatformChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnLaunchProfileChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref _launchProfile, nameof(LaunchProfile));

            if (oldValue != newValue)
            {
                BubbleEvent(new LaunchProfileChangedEvent(this, oldValue, newValue));
            }
        }

        public void ToggleCheckedState()
        {
            if (IsChecked == null)
                IsChecked = false;
            else
                IsChecked = !IsChecked;
        }

        /// <summary>
        /// Check this item and uncheck every other.
        /// </summary>
        private void ExclusiveChecked()
        {
            var checkedItems = Parent.Items.Where(item => item.IsChecked != false).ToList();
            var checkState = checkedItems.Count != 1 || isChecked != true;

            foreach (var item in checkedItems)
            {
                item.OnIsCheckedChanged(item.IsChecked, newValue: false, notifyParent: false);
            }

            OnIsCheckedChanged(isChecked, checkState, notifyParent:true);
        }

        protected virtual void OnChildIsCheckedChanged(bool? oldValue, bool? newValue)
        {}

        public void SetIsCheckedWithoutNotifyingParent(bool? value)
        {
            OnIsCheckedChanged(IsChecked, value, false);
        }

        public void SetIsCheckedAndNotifyingParent(bool? value)
        {
            OnIsCheckedChanged(IsChecked, value, true);
        }

        #region Editing
        private string editBackupValue;

        private bool isInEditMode;
        public bool IsInEditMode { get => isInEditMode; private set => SetAndNotify(value, ref this.isInEditMode); }

        public event EventHandler<EditModeChangedEventArgs> EditModeChanged;
        public enum EditMode
        {
            BeganEdit, BeganEditAndReset, CanceledEdit, CommitedEdit
        }

        public class EditModeChangedEventArgs : EventArgs
        {
            public EditMode Mode { get; }

            public EditModeChangedEventArgs(EditMode mode)
            {
                Mode = mode;
            }
        }

        public void BeginEdit(string initialValue = null)
        {
            ThrowIfNotEditable();

            if (!IsInEditMode)
            {
                editBackupValue = Value;
                if (initialValue != null)
                    Value = initialValue;

                IsInEditMode = true;
                EditModeChanged?.Invoke(this, new EditModeChangedEventArgs(initialValue != null ? EditMode.BeganEditAndReset : EditMode.BeganEdit));
                BubbleEvent(new ItemEditModeChangedEvent(this, isInEditMode: true));
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
                EditModeChanged?.Invoke(this, new EditModeChangedEventArgs(EditMode.CanceledEdit));
                BubbleEvent(new ItemEditModeChangedEvent(this, isInEditMode: false));
            }
        }

        public void CommitEdit()
        {
            ThrowIfNotEditable();

            if (IsInEditMode)
            {
                editBackupValue = null;

                IsInEditMode = false;
                EditModeChanged?.Invoke(this, new EditModeChangedEventArgs(EditMode.CommitedEdit));
                BubbleEvent(new ItemEditModeChangedEvent(this, isInEditMode: false));
            }
        }

        private void ThrowIfNotEditable()
        {
            if (!IsEditable)
                throw new InvalidOperationException("Can't execute edit operation on a not editable item!");
        }
#endregion

        public virtual CmdBase Copy()
        {
            return null;
        }

        public override string ToString()
        {
            return $"{this.GetType().Name}{{{Value}:{(IsChecked == null ? "▄" : (IsChecked.Value ? "☑" : "☐"))}:{(IsSelected ? "█" : "-")}}}";
        }
    }
}
