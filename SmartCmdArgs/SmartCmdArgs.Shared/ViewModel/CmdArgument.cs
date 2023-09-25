using System;

namespace SmartCmdArgs.ViewModel
{
    public enum ArgumentType
    {
        CmdArg,
        EnvVar,
        WorkDir,
    }

    public class CmdArgument : CmdBase
    {
        public override bool IsEditable => true;

        public new bool IsChecked
        {
            get => base.IsChecked == true;
            set => base.IsChecked = value;
        }

        private bool isActive = true;
        public new bool IsActive { get => isActive; set => SetAndNotify(value, ref isActive); }

        private ArgumentType argumentType;
        public ArgumentType ArgumentType { get => argumentType; set => OnArgumentTypeChanged(argumentType, value); }

        private bool defaultChecked;
        public bool DefaultChecked { get => defaultChecked; set => OnDefaultCheckedChanged(defaultChecked, value); }

        public CmdArgument(Guid id, ArgumentType argType, string arg, bool isChecked = false, bool defaultChecked = false)
            : base(id, arg, isChecked)
        {
            this.argumentType = argType;
            this.defaultChecked = defaultChecked;
        }

        public CmdArgument(ArgumentType argType, string arg, bool isChecked = false, bool defaultChecked = false)
            : this(Guid.NewGuid(), argType, arg, isChecked, defaultChecked)
        { }

        private void OnDefaultCheckedChanged(bool oldValue, bool newValue)
        {
            SetAndNotify(newValue, ref defaultChecked, nameof(DefaultChecked));

            if (oldValue != newValue)
            {
                BubbleEvent(new DefaultCheckedChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnArgumentTypeChanged(ArgumentType oldValue, ArgumentType newValue)
        {
            SetAndNotify(newValue, ref argumentType, nameof(ArgumentType));

            if (oldValue != newValue)
            {
                BubbleEvent(new ArgumentTypeChangedEvent(this, oldValue, newValue));
            }
        }

        public override CmdBase Copy()
        {
            return new CmdArgument(ArgumentType, Value, IsChecked, DefaultChecked);
        }
    }
}
