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
        private CmdArgsPackage _package;

        private bool? _saveSettingsToJson;
        private bool? _useCustomJsonRoot;
        private string _jsonRootPath;
        private bool? _vcsSupportEnabled;
        private bool? _useSolutionDir;
        private bool? _macroEvaluationEnabled;

        public bool? SaveSettingsToJson
        {
            get => _saveSettingsToJson;
            set => SetAndNotify(value, ref _saveSettingsToJson);
        }

        public bool? UseCustomJsonRoot
        {
            get => _useCustomJsonRoot;
            set => SetAndNotify(value, ref _useCustomJsonRoot);
        }

        public string JsonRootPath
        {
            get => _jsonRootPath;
            set => SetAndNotify(value, ref _jsonRootPath);
        }

        public bool? VcsSupportEnabled
        {
            get => _vcsSupportEnabled;
            set => SetAndNotify(value, ref _vcsSupportEnabled);
        }

        public bool? UseSolutionDir
        {
            get => _useSolutionDir;
            set => SetAndNotify(value, ref _useSolutionDir);
        }

        public bool? MacroEvaluationEnabled
        {
            get => _macroEvaluationEnabled;
            set => SetAndNotify(value, ref _macroEvaluationEnabled);
        }

        public SettingsViewModel() { }

        public SettingsViewModel(SettingsViewModel other)
        {
            Assign(other);
        }

        public void Assign(SettingsViewModel other)
        {
            typeof(SettingsViewModel)
                .GetProperties()
                .ForEach(p => p.SetValue(this, p.GetValue(other)));
        }
    }
}
