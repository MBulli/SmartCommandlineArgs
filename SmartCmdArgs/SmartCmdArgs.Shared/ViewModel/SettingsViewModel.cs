using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SmartCmdArgs.ViewModel
{
    public class SettingsViewModel : PropertyChangedBase
    {
        private CmdArgsPackage _package;

        private bool? _saveSettingsToJson;
        private bool? _vcsSupportEnabled;
        private bool? _useSolutionDir;
        private bool? _macroEvaluationEnabled;
        private bool? _CPSCustomProjectEnabled;

        public bool? SaveSettingsToJson
        {
            get => _saveSettingsToJson;
            set => SetAndNotify(value, ref _saveSettingsToJson);
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


        public bool? CPSCustomProjectEnabled
        {
            get => _CPSCustomProjectEnabled;
            set => SetAndNotify(value, ref _CPSCustomProjectEnabled);
        }
        public RelayCommand OpenOptionsCommand { get; }

        public SettingsViewModel(CmdArgsPackage package)
        {
            _package = package;

            OpenOptionsCommand = new RelayCommand(() =>
            {
                package.ShowOptionPage(typeof(CmdArgsOptionPage));
            });
        }

        public SettingsViewModel(SettingsViewModel other) : this(other._package)
        {
            Assign(other);
        }

        public void Assign(SettingsViewModel other)
        {
            typeof(SettingsViewModel)
                .GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ForEach(p => p.SetValue(this, p.GetValue(other)));
        }
    }
}
