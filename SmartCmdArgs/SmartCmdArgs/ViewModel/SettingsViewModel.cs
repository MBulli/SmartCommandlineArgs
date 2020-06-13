using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.ViewModel
{
    public class SettingsViewModel
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
                    _vcsSupportEnabled = value;
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
                    _useSolutionDir = value;
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
                    _macroEvaluationEnabled = value;
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
