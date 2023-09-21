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
        public bool SaveSettingsToJson => settingsViewModel.SaveSettingsToJson;
        public bool ManageCommandLineArgs => settingsViewModel.ManageCommandLineArgs ?? optionsPage.ManageCommandLineArgs;
        public bool ManageEnvironmentVars => settingsViewModel.ManageEnvironmentVars ?? optionsPage.ManageEnvironmentVars;
        public bool ManageWorkingDirectories => settingsViewModel.ManageWorkingDirectories ?? optionsPage.ManageWorkingDirectories;
        public bool UseCustomJsonRoot => settingsViewModel.UseCustomJsonRoot;
        public string JsonRootPath => settingsViewModel.JsonRootPath;
        public bool VcsSupportEnabled => settingsViewModel.VcsSupportEnabled ?? optionsPage.VcsSupportEnabled;
        public bool MacroEvaluationEnabled => settingsViewModel.MacroEvaluationEnabled ?? optionsPage.MacroEvaluationEnabled;
        public bool UseSolutionDir => _vsHelperService?.GetSolutionFilename() != null && (settingsViewModel.UseSolutionDir ?? optionsPage.UseSolutionDir);

        // Options
        public bool UseMonospaceFont => optionsPage.UseMonospaceFont;
        public bool DisplayTagForCla => optionsPage.DisplayTagForCla;
        public bool DeleteEmptyFilesAutomatically => optionsPage.DeleteEmptyFilesAutomatically;
        public bool DeleteUnnecessaryFilesAutomatically => optionsPage.DeleteUnnecessaryFilesAutomatically;
        public bool EnabledByDefault => optionsPage.EnabledByDefault;
        public InactiveDisableMode DisableInactiveItems => optionsPage.DisableInactiveItems;
        public RelativePathRootOption RelativePathRoot => optionsPage.RelativePathRoot;


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
            settingsViewModel.PropertyChanged += Settings_PropertyChanged;
            optionsPage.PropertyChanged += Options_PropertyChanged;

            return Task.CompletedTask;
        }
    }
}
