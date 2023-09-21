﻿using Microsoft.VisualStudio.Settings;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;

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

        public OptionsSettingsEventHandlingService(
            IOptionsSettingsService optionsSettings,
            ISettingsService settingsService,
            IFileStorageService fileStorage,
            IVisualStudioHelperService vsHelper,
            IViewModelUpdateService viewModelUpdateService,
            ToolWindowViewModel toolWindowViewModel)
        {
            this.optionsSettings = optionsSettings;
            this.settingsService = settingsService;
            this.fileStorage = fileStorage;
            this.vsHelper = vsHelper;
            this.viewModelUpdateService = viewModelUpdateService;
            this.toolWindowViewModel = toolWindowViewModel;
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
                case nameof(IOptionsSettingsService.ManageCommandLineArgs): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageEnvironmentVars): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageWorkingDirectories): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.UseMonospaceFont): UseMonospaceFontChanged(); break;
                case nameof(IOptionsSettingsService.DisplayTagForCla): DisplayTagForClaChanged(); break;
                case nameof(IOptionsSettingsService.DisableInactiveItems): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
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

            ToolWindowHistory.SaveState();

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                viewModelUpdateService.UpdateCommandsForProject(project);
            }
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