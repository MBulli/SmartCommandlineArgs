using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using SmartCmdArgs.Services;
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
        private readonly CmdArgsOptionPage optionPage;
        private bool _saveSettingsToJson;
        private bool? _manageCommandLineArgs;
        private bool? _manageEnvironmentVars;
        private bool? _manageWorkingDirectories;
        private bool _useCustomJsonRoot;
        private string _jsonRootPath;
        private bool? _vcsSupportEnabled;
        private bool? _useSolutionDir;
        private bool? _macroEvaluationEnabled;

        public bool SaveSettingsToJson
        {
            get => _saveSettingsToJson;
            set => SetAndNotify(value, ref _saveSettingsToJson);
        }

        public bool? ManageCommandLineArgs
        {
            get => _manageCommandLineArgs;
            set => SetAndNotify(value, ref _manageCommandLineArgs);
        }

        public bool? ManageEnvironmentVars
        {
            get => _manageEnvironmentVars;
            set => SetAndNotify(value, ref _manageEnvironmentVars);
        }

        public bool? ManageWorkingDirectories
        {
            get => _manageWorkingDirectories;
            set => SetAndNotify(value, ref _manageWorkingDirectories);
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

        public bool UseCustomJsonRoot
        {
            get => _useCustomJsonRoot;
            set => SetAndNotify(value, ref _useCustomJsonRoot);
        }

        public string JsonRootPath
        {
            get => _jsonRootPath;
            set => SetAndNotify(value, ref _jsonRootPath);
        }

        public bool? MacroEvaluationEnabled
        {
            get => _macroEvaluationEnabled;
            set => SetAndNotify(value, ref _macroEvaluationEnabled);
        }

        public RelayCommand DisableExtensionCommand { get; }

        public CmdArgsPackage Package => CmdArgsPackage.Instance;

        public CmdArgsOptionPage Options => optionPage;

        public RelayCommand OpenOptionsCommand { get; }

        public SettingsViewModel(CmdArgsOptionPage optionPage)
        {
            this.optionPage = optionPage;

            DisableExtensionCommand = new RelayCommand(() =>
            {
                Package.IsEnabledSaved = false;
            });

            OpenOptionsCommand = new RelayCommand(() =>
            {
                CmdArgsPackage.Instance.ShowOptionPage(typeof(CmdArgsOptionPage));
            });
        }

        public void Assign(SettingsViewModel other)
        {
            using (PauseAndStoreNotifications())
            {
                typeof(SettingsViewModel)
                    .GetProperties()
                    .Where(p => p.CanRead && p.CanWrite)
                    .ForEach(p => p.SetValue(this, p.GetValue(other)));
            }
        }

        public void Assign(SettingsJson other)
        {
            using (PauseAndStoreNotifications())
            {
                ManageCommandLineArgs = other.ManageCommandLineArgs;
                ManageEnvironmentVars = other.ManageEnvironmentVars;
                ManageWorkingDirectories = other.ManageWorkingDirectories;
                UseCustomJsonRoot = other.UseCustomJsonRoot;
                JsonRootPath = other.JsonRootPath;
                VcsSupportEnabled = other.VcsSupportEnabled;
                UseSolutionDir = other.UseSolutionDir;
                MacroEvaluationEnabled = other.MacroEvaluationEnabled;
            }
        }
    }
}
