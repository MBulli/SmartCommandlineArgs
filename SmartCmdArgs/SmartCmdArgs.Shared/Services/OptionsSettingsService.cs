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
        private readonly ISettingsService settings;
        private readonly CmdArgsOptionPage optionsPage;

        public OptionsSettingsService(
            IVisualStudioHelperService vsHelperService,
            ISettingsService settings,
            CmdArgsOptionPage optionsPage)
        {
            _vsHelperService = vsHelperService;
            this.settings = settings;
            this.optionsPage = optionsPage;
        }

        // Settings (possibly with options default)
        public bool SaveSettingsToJson => settings.ViewModel.SaveSettingsToJson;
        public bool ManageCommandLineArgs => settings.ViewModel.ManageCommandLineArgs ?? optionsPage.ManageCommandLineArgs;
        public bool ManageEnvironmentVars => settings.ViewModel.ManageEnvironmentVars ?? optionsPage.ManageEnvironmentVars;
        public bool ManageWorkingDirectories => settings.ViewModel.ManageWorkingDirectories ?? optionsPage.ManageWorkingDirectories;
        public bool UseCustomJsonRoot => settings.ViewModel.UseCustomJsonRoot;
        public string JsonRootPath => settings.ViewModel.JsonRootPath;
        public bool VcsSupportEnabled => settings.ViewModel.VcsSupportEnabled ?? optionsPage.VcsSupportEnabled;
        public bool MacroEvaluationEnabled => settings.ViewModel.MacroEvaluationEnabled ?? optionsPage.MacroEvaluationEnabled;
        public bool UseSolutionDir => _vsHelperService?.GetSolutionFilename() != null && (settings.ViewModel.UseSolutionDir ?? optionsPage.UseSolutionDir);

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
            settings.ViewModel.PropertyChanged += Settings_PropertyChanged;
            optionsPage.PropertyChanged += Options_PropertyChanged;

            return Task.CompletedTask;
        }
    }
}
