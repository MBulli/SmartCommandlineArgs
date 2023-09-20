using SmartCmdArgs.Helper;
using SmartCmdArgs.View;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Services
{
    public interface IOptionsSettingsService
    {
        // Settings (possibly with options default)
        SettingsViewModel Settings { get; }
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
        CmdArgsOptionPage Options { get; }
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
        private readonly IVisualStudioHelperService _vsHelperService;
        private readonly SettingsViewModel settingsViewModel;
        private readonly CmdArgsOptionPage optionsPage;

        public OptionsSettingsService(
            IVisualStudioHelperService vsHelperService,
            SettingsViewModel settingsViewModel,
            CmdArgsOptionPage optionsPage)
        {
            _vsHelperService = vsHelperService;
            this.settingsViewModel = settingsViewModel;
            this.optionsPage = optionsPage;
        }

        // Settings (possibly with options default)
        public SettingsViewModel Settings => settingsViewModel;
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
        public CmdArgsOptionPage Options => optionsPage;
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
