using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.ViewModel
{
    public class SettingsViewModel : PropertyChangedBase
    {
        private bool _vcsSupportEnabled;
        private bool _useSolutionDir;
        private bool _macroEvaluationEnabled;

        public bool VcsSupportEnabled
        {
            get => _vcsSupportEnabled;
            set
            {
                if (_vcsSupportEnabled != value)
                {
                    SetAndNotify(value, ref _vcsSupportEnabled);
                    VcsSupportEnabledChanged?.Invoke(this, value);
                }
            }
        }

        public bool UseSolutionDir
        {
            get => _useSolutionDir;
            set
            {
                if (_useSolutionDir != value)
                {
                    SetAndNotify(value, ref _useSolutionDir);
                    UseSolutionDirChanged?.Invoke(this, value);
                }
            }
        }

        public bool MacroEvaluationEnabled
        {
            get => _macroEvaluationEnabled;
            set
            {
                if (_macroEvaluationEnabled != value)
                {
                    SetAndNotify(value, ref _macroEvaluationEnabled);
                    MacroEvaluationEnabledChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<bool> VcsSupportEnabledChanged;
        public event EventHandler<bool> UseSolutionDirChanged;
        public event EventHandler<bool> MacroEvaluationEnabledChanged;

        public SettingsViewModel() { }

        public SettingsViewModel(SettingsViewModel other)
        {
            Assign(other);
        }

        public void Assign(SettingsViewModel other)
        {
            VcsSupportEnabled = other.VcsSupportEnabled;
            UseSolutionDir = other.UseSolutionDir;
            MacroEvaluationEnabled = other.MacroEvaluationEnabled;
        }
    }
}
