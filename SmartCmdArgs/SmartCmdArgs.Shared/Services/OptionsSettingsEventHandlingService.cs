using SmartCmdArgs.ViewModel;
using System;

namespace SmartCmdArgs.Services
{
    internal interface IOptionsSettingsEventHandlingService : IDisposable
    {
        void AttachToEvents();
        void DetachFromEvents();
    }

    internal class OptionsSettingsEventHandlingService : IOptionsSettingsEventHandlingService
    {
        private readonly IOptionsSettingsService optionsSettings;
        private readonly ISettingsService settingsService;
        private readonly IFileStorageService fileStorage;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly IViewModelUpdateService viewModelUpdateService;
        private readonly ToolWindowViewModel toolWindowViewModel;
        private readonly IToolWindowHistory toolWindowHistory;

        public OptionsSettingsEventHandlingService(
            IOptionsSettingsService optionsSettings,
            ISettingsService settingsService,
            IFileStorageService fileStorage,
            IVisualStudioHelperService vsHelper,
            IViewModelUpdateService viewModelUpdateService,
            ToolWindowViewModel toolWindowViewModel,
            IToolWindowHistory toolWindowHistory)
        {
            this.optionsSettings = optionsSettings;
            this.settingsService = settingsService;
            this.fileStorage = fileStorage;
            this.vsHelper = vsHelper;
            this.viewModelUpdateService = viewModelUpdateService;
            this.toolWindowViewModel = toolWindowViewModel;
            this.toolWindowHistory = toolWindowHistory;
        }

        public void Dispose()
        {
            DetachFromEvents();
        }

        public void AttachToEvents()
        {
            optionsSettings.PropertyChanged += OptionsSettings_PropertyChanged;
        }

        public void DetachFromEvents()
        {
            optionsSettings.PropertyChanged -= OptionsSettings_PropertyChanged;
        }

        private void OptionsSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!settingsService.Loaded)
                return;

            switch (e.PropertyName)
            {
                case nameof(IOptionsSettingsService.SaveSettingsToJson): SaveSettingsToJsonChanged(); break;
                case nameof(IOptionsSettingsService.UseCustomJsonRoot): UseCustomJsonRootChanged(); break;
                case nameof(IOptionsSettingsService.JsonRootPath): JsonRootPathChanged(); break;
                case nameof(IOptionsSettingsService.VcsSupportEnabled): VcsSupportChanged(); break;
                case nameof(IOptionsSettingsService.UseSolutionDir): UseSolutionDirChanged(); break;
                case nameof(IOptionsSettingsService.ManageCommandLineArgs): viewModelUpdateService.UpdateIsActiveForParamsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageEnvironmentVars): viewModelUpdateService.UpdateIsActiveForParamsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageWorkingDirectories): viewModelUpdateService.UpdateIsActiveForParamsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageLaunchApplication): viewModelUpdateService.UpdateIsActiveForParamsDebounced(); break;
                case nameof(IOptionsSettingsService.UseMonospaceFont): UseMonospaceFontChanged(); break;
                case nameof(IOptionsSettingsService.DisplayTagForCla): DisplayTagForClaChanged(); break;
                case nameof(IOptionsSettingsService.DisableInactiveItems): viewModelUpdateService.UpdateIsActiveForParamsDebounced(); break;
            }
        }

        private void SaveSettingsToJsonChanged()
        {
            settingsService.Save();
        }

        private void UseCustomJsonRootChanged()
        {
            fileStorage.SaveAllProjects();
        }

        private void JsonRootPathChanged()
        {
            fileStorage.SaveAllProjects();
        }

        private void VcsSupportChanged()
        {
            if (!optionsSettings.VcsSupportEnabled)
                return;

            toolWindowHistory.SaveState();

            viewModelUpdateService.UpdateCommandsForAllProjects();

            fileStorage.SaveAllProjects();
        }

        private void UseMonospaceFontChanged()
        {
            toolWindowViewModel.UseMonospaceFont = optionsSettings.UseMonospaceFont;
        }

        private void DisplayTagForClaChanged()
        {
            toolWindowViewModel.DisplayTagForCla = optionsSettings.DisplayTagForCla;
        }

        private void UseSolutionDirChanged()
        {
            fileStorage.DeleteAllUnusedArgFiles();
            fileStorage.SaveAllProjects();
        }
    }
}
