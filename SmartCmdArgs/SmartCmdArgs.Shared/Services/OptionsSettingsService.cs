using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Services
{
    internal interface IOptionsSettingsService
    {
        // Settings (possibly with options default)
        bool SaveSettingsToJson { get; }
        bool ManageCommandLineArgs { get; }
        bool ManageEnvironmentVars { get; }
        bool ManageWorkingDirectories { get; }
        bool UseCustomJsonRoot { get; }
        string JsonRootPath { get; }
        bool VcsSupportEnabled { get; }
        bool MacroEvaluationEnabled { get; }
        bool UseSolutionDir { get; }

        // Options
        bool UseMonospaceFont { get; }
        bool DisplayTagForCla { get; }
        bool DeleteEmptyFilesAutomatically { get; }
        bool DeleteUnnecessaryFilesAutomatically { get; }
        bool EnabledByDefault { get; }
        InactiveDisableMode DisableInactiveItems { get; }
        RelativePathRootOption RelativePathRoot {  get; }

        // Events
        event PropertyChangedEventHandler PropertyChanged;
    }

    internal class OptionsSettingsService : PropertyChangedBase, IOptionsSettingsService, IAsyncInitializable
    {
        readonly IVisualStudioHelperService _vsHelperService;

        public OptionsSettingsService(IVisualStudioHelperService vsHelperService)
        {
            _vsHelperService = vsHelperService;
        }

        readonly CmdArgsPackage _package = CmdArgsPackage.Instance;

        public SettingsViewModel Settings => _package.ToolWindowViewModel.SettingsViewModel;
        public CmdArgsOptionPage Options => _package.GetDialogPage<CmdArgsOptionPage>();

        // Settings (possibly with options default)
        public bool SaveSettingsToJson => Settings.SaveSettingsToJson;
        public bool ManageCommandLineArgs => Settings.ManageCommandLineArgs ?? Options.ManageCommandLineArgs;
        public bool ManageEnvironmentVars => Settings.ManageEnvironmentVars ?? Options.ManageEnvironmentVars;
        public bool ManageWorkingDirectories => Settings.ManageWorkingDirectories ?? Options.ManageWorkingDirectories;
        public bool UseCustomJsonRoot => Settings.UseCustomJsonRoot;
        public string JsonRootPath => Settings.JsonRootPath;
        public bool VcsSupportEnabled => Settings.VcsSupportEnabled ?? Options.VcsSupportEnabled;
        public bool MacroEvaluationEnabled => Settings.MacroEvaluationEnabled ?? Options.MacroEvaluationEnabled;
        public bool UseSolutionDir => _vsHelperService?.GetSolutionFilename() != null && (Settings.UseSolutionDir ?? Options.UseSolutionDir);
        
        // Options
        public bool UseMonospaceFont => Options.UseMonospaceFont;
        public bool DisplayTagForCla => Options.DisplayTagForCla;
        public bool DeleteEmptyFilesAutomatically => Options.DeleteEmptyFilesAutomatically;
        public bool DeleteUnnecessaryFilesAutomatically => Options.DeleteUnnecessaryFilesAutomatically;
        public bool EnabledByDefault => Options.EnabledByDefault;
        public InactiveDisableMode DisableInactiveItems => Options.DisableInactiveItems;
        public RelativePathRootOption RelativePathRoot => Options.RelativePathRoot;


        // Event Handlers
        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnNotifyPropertyChanged(e.PropertyName);
        }

        private void Options_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnNotifyPropertyChanged(e.PropertyName);
        }

        public Task InitializeAsync()
        {
            Settings.PropertyChanged += Settings_PropertyChanged;
            Options.PropertyChanged += Options_PropertyChanged;

            return Task.CompletedTask;
        }
    }
}
