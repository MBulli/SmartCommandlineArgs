using System;

namespace SmartCmdArgs.ViewModel
{
    public enum CmdParamType
    {
        CmdArg,
        EnvVar,
        WorkDir,
        LaunchApp,
    }

    public class CmdParameter : CmdBase
    {
        public override bool IsEditable => true;

        public new bool IsChecked
        {
            get => base.IsChecked == true;
            set => base.IsChecked = value;
        }

        private bool isActive = true;
        public new bool IsActive { get => isActive; set => SetAndNotify(value, ref isActive); }

        private CmdParamType paramType;
        public CmdParamType ParamType { get => paramType; set => OnParamTypeChanged(paramType, value); }

        private bool defaultChecked;
        public bool DefaultChecked { get => defaultChecked; set => OnDefaultCheckedChanged(defaultChecked, value); }

        public CmdParameter(Guid id, CmdParamType paramType, string value, bool isChecked = false, bool defaultChecked = false)
            : base(id, value, isChecked)
        {
            this.paramType = paramType;
            this.defaultChecked = defaultChecked;
        }

        public CmdParameter(CmdParamType paramType, string value, bool isChecked = false, bool defaultChecked = false)
            : this(Guid.NewGuid(), paramType, value, isChecked, defaultChecked)
        { }

        private void OnDefaultCheckedChanged(bool oldValue, bool newValue)
        {
            SetAndNotify(newValue, ref defaultChecked, nameof(DefaultChecked));

            if (oldValue != newValue)
            {
                BubbleEvent(new DefaultCheckedChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnParamTypeChanged(CmdParamType oldValue, CmdParamType newValue)
        {
            SetAndNotify(newValue, ref paramType, nameof(ParamType));

            if (oldValue != newValue)
            {
                BubbleEvent(new ParamTypeChangedEvent(this, oldValue, newValue));
            }
        }

        public override CmdBase Copy()
        {
            return new CmdParameter(ParamType, Value, IsChecked, DefaultChecked);
        }
    }
}
