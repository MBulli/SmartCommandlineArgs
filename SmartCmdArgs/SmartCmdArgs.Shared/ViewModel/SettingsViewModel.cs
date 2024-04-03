using SmartCmdArgs.Helper;
using SmartCmdArgs.DataSerialization;
using SmartCmdArgs.Services;
using System;
using System.Linq;

namespace SmartCmdArgs.ViewModel
{
    public class SettingsViewModel : PropertyChangedBase
    {
        private readonly Lazy<CmdArgsOptionPage> optionPage;
        private readonly Lazy<ILifeCycleService> lifeCycleService;
        private bool _saveSettingsToJson;
        private bool? _manageCommandLineArgs;
        private bool? _manageEnvironmentVars;
        private bool? _manageWorkingDirectories;
        private bool? _manageLaunchApplication;
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

        public bool? ManageLaunchApplication
        {
            get => _manageLaunchApplication;
            set => SetAndNotify(value, ref _manageLaunchApplication);
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

        public CmdArgsOptionPage Options => optionPage.Value;
        public ILifeCycleService LifeCycle => lifeCycleService.Value;

        public RelayCommand OpenOptionsCommand { get; }

        public SettingsViewModel(
            Lazy<CmdArgsOptionPage> optionPage,
            Lazy<ILifeCycleService> lifeCycleService)
        {
            this.optionPage = optionPage;
            this.lifeCycleService = lifeCycleService;

            DisableExtensionCommand = new RelayCommand(() =>
            {
                lifeCycleService.Value.IsEnabledSaved = false;
            });

            OpenOptionsCommand = new RelayCommand(() =>
            {
                CmdArgsPackage.Instance.ShowOptionPage(typeof(CmdArgsOptionPage));
            });
        }

        public SettingsViewModel Clone()
        {
            var clone = new SettingsViewModel(optionPage, lifeCycleService);
            clone.Assign(this);
            return clone;
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
                ManageLaunchApplication = other.ManageLaunchApplication;
                UseCustomJsonRoot = other.UseCustomJsonRoot;
                JsonRootPath = other.JsonRootPath;
                VcsSupportEnabled = other.VcsSupportEnabled;
                UseSolutionDir = other.UseSolutionDir;
                MacroEvaluationEnabled = other.MacroEvaluationEnabled;
            }
        }
    }
}
