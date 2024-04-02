using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;
using System.ComponentModel;
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
        bool ManageLaunchApplication { get; }
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
        EnableBehaviour EnableBehaviour { get; }
        InactiveDisableMode DisableInactiveItems { get; }
        RelativePathRootOption RelativePathRoot {  get; }

        // Events

        event PropertyChangedEventHandler PropertyChanged;
    }

    internal class OptionsSettingsService : PropertyChangedBase, IOptionsSettingsService, IAsyncInitializable
    {
        private readonly IVisualStudioHelperService _vsHelperService;
        private readonly SettingsViewModel settingsViewModel;
        private readonly CmdArgsOptionPage optionPage;

        public OptionsSettingsService(
            IVisualStudioHelperService vsHelperService,
            SettingsViewModel settingsViewModel,
            CmdArgsOptionPage optionPage)
        {
            _vsHelperService = vsHelperService;
            this.settingsViewModel = settingsViewModel;
            this.optionPage = optionPage;
        }

        private CmdArgsOptionPage OptionsPage => optionPage;

        // Settings (possibly with options default)
        public bool SaveSettingsToJson => settingsViewModel.SaveSettingsToJson;
        public bool ManageCommandLineArgs => settingsViewModel.ManageCommandLineArgs ?? OptionsPage.ManageCommandLineArgs;
        public bool ManageEnvironmentVars => settingsViewModel.ManageEnvironmentVars ?? OptionsPage.ManageEnvironmentVars;
        public bool ManageWorkingDirectories => settingsViewModel.ManageWorkingDirectories ?? OptionsPage.ManageWorkingDirectories;
        public bool ManageLaunchApplication => settingsViewModel.ManageLaunchApplication ?? OptionsPage.ManageLaunchApplication;
        public bool UseCustomJsonRoot => settingsViewModel.UseCustomJsonRoot;
        public string JsonRootPath => settingsViewModel.JsonRootPath;
        public bool VcsSupportEnabled => settingsViewModel.VcsSupportEnabled ?? OptionsPage.VcsSupportEnabled;
        public bool MacroEvaluationEnabled => settingsViewModel.MacroEvaluationEnabled ?? OptionsPage.MacroEvaluationEnabled;
        public bool UseSolutionDir => _vsHelperService?.GetSolutionFilename() != null && (settingsViewModel.UseSolutionDir ?? OptionsPage.UseSolutionDir);

        // Options
        public bool UseMonospaceFont => OptionsPage.UseMonospaceFont;
        public bool DisplayTagForCla => OptionsPage.DisplayTagForCla;
        public bool DeleteEmptyFilesAutomatically => OptionsPage.DeleteEmptyFilesAutomatically;
        public bool DeleteUnnecessaryFilesAutomatically => OptionsPage.DeleteUnnecessaryFilesAutomatically;
        public EnableBehaviour EnableBehaviour => OptionsPage.EnableBehaviour;
        public InactiveDisableMode DisableInactiveItems => OptionsPage.DisableInactiveItems;
        public RelativePathRootOption RelativePathRoot => OptionsPage.RelativePathRoot;


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
            OptionsPage.PropertyChanged += Options_PropertyChanged;

            return Task.CompletedTask;
        }
    }
}
