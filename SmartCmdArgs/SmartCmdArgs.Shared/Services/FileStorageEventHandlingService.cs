using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Wrapper;
using System;
using System.Collections.Generic;

namespace SmartCmdArgs.Services
{
    internal interface IFileStorageEventHandlingService : IDisposable
    {
        void AttachToEvents();
        void DetachFromEvents();
    }

    internal class FileStorageEventHandlingService : IFileStorageEventHandlingService
    {
        private readonly IFileStorageService fileStorage;
        private readonly IOptionsSettingsService optionsSettings;
        private readonly ISettingsService settingsService;
        private readonly ILifeCycleService lifeCycleService;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly IViewModelUpdateService viewModelUpdateService;
        private readonly IToolWindowHistory toolWindowHistory;

        public FileStorageEventHandlingService(
            IFileStorageService fileStorage,
            IOptionsSettingsService optionsSettings,
            ISettingsService settingsService,
            ILifeCycleService lifeCycleService,
            IVisualStudioHelperService vsHelper,
            IViewModelUpdateService viewModelUpdateService,
            IToolWindowHistory toolWindowHistory)
        {
            this.fileStorage = fileStorage;
            this.optionsSettings = optionsSettings;
            this.settingsService = settingsService;
            this.lifeCycleService = lifeCycleService;
            this.vsHelper = vsHelper;
            this.viewModelUpdateService = viewModelUpdateService;
            this.toolWindowHistory = toolWindowHistory;
        }

        public void Dispose()
        {
            DetachFromEvents();
        }

        public void AttachToEvents()
        {
            fileStorage.FileStorageChanged += FileStorage_FileStorageChanged;
        }

        public void DetachFromEvents()
        {
            fileStorage.FileStorageChanged += FileStorage_FileStorageChanged;
        }

        private void FileStorage_FileStorageChanged(object sender, FileStorageChangedEventArgs e)
        {
            // This event is triggered on non-main thread!

            Logger.Info($"Dispatching update commands function call");

            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                // git branch and merge might lead to a race condition here.
                // If a branch is checkout where the json file differs, the
                // filewatcher will trigger an event which is dispatched here.
                // However, while the function call is queued VS may reopen the
                // solution due to changes. This will ultimately result in a
                // null ref exception because the project object is unloaded.
                // UpdateCommandsForProject() will skip such projects because
                // their guid is empty.

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (e.Type == FileStorageChanedType.Settings)
                {
                    if (optionsSettings.SaveSettingsToJson)
                        settingsService.Load();

                    return;
                }

                if (!lifeCycleService.IsEnabled)
                    return;

                if (e.IsSolutionWide != optionsSettings.UseSolutionDir)
                    return;

                if (!optionsSettings.VcsSupportEnabled)
                    return;

                toolWindowHistory.SaveState();

                IEnumerable<IVsHierarchyWrapper> projects;
                if (e.IsSolutionWide)
                {
                    Logger.Info($"Dispatched update commands function calls for the solution.");
                    projects = vsHelper.GetSupportedProjects();
                }
                else
                {
                    Logger.Info($"Dispatched update commands function call for project '{e.Project.GetDisplayName()}'");
                    projects = new[] { e.Project };
                }

                viewModelUpdateService.UpdateCommandsForAllProjects();

                viewModelUpdateService.UpdateIsActiveForParamsDebounced();
            });
        }
    }
}
