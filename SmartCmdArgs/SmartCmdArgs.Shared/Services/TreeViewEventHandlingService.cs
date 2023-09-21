using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartCmdArgs.Services
{
    internal interface ITreeViewEventHandlingService : IDisposable
    {
        void AttachToEvents();
        void DetachFromEvents();
    }

    internal class TreeViewEventHandlingService : ITreeViewEventHandlingService
    {
        private readonly ToolWindowViewModel toolWindowViewModel;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly IOptionsSettingsService optionsSettings;
        private readonly IFileStorageService fileStorage;
        private readonly IProjectConfigService projectConfigService;
        private readonly IViewModelUpdateService viewModelUpdateService;

        public TreeViewEventHandlingService(
            ToolWindowViewModel toolWindowViewModel,
            IVisualStudioHelperService vsHelper,
            IOptionsSettingsService optionsSettings,
            IFileStorageService fileStorage,
            IProjectConfigService projectConfigService,
            IViewModelUpdateService viewModelUpdateService)
        {
            this.toolWindowViewModel = toolWindowViewModel;
            this.vsHelper = vsHelper;
            this.optionsSettings = optionsSettings;
            this.fileStorage = fileStorage;
            this.projectConfigService = projectConfigService;
            this.viewModelUpdateService = viewModelUpdateService;
        }

        public void Dispose()
        {
            DetachFromEvents();
        }

        public void AttachToEvents()
        {
            toolWindowViewModel.TreeViewModel.ItemSelectionChanged += OnItemSelectionChanged;
            toolWindowViewModel.TreeViewModel.TreeContentChangedThrottled += OnTreeContentChangedThrottled;
            toolWindowViewModel.TreeViewModel.TreeChangedThrottled += OnTreeChangedThrottled;
            toolWindowViewModel.TreeViewModel.TreeChanged += OnTreeChanged;
        }

        public void DetachFromEvents()
        {
            toolWindowViewModel.TreeViewModel.ItemSelectionChanged -= OnItemSelectionChanged;
            toolWindowViewModel.TreeViewModel.TreeContentChangedThrottled -= OnTreeContentChangedThrottled;
            toolWindowViewModel.TreeViewModel.TreeChangedThrottled -= OnTreeChangedThrottled;
            toolWindowViewModel.TreeViewModel.TreeChanged -= OnTreeChanged;
        }

        private void OnItemSelectionChanged(object sender, CmdBase cmdBase)
        {
            vsHelper.UpdateShellCommandUI(false);
        }

        private void OnTreeContentChangedThrottled(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            if (optionsSettings.VcsSupportEnabled)
            {
                Logger.Info($"Tree content changed and VCS support is enabled. Saving all project commands to json file for project '{e.AffectedProject.Id}'.");

                var projectGuid = e.AffectedProject.Id;

                try
                {
                    var project = vsHelper.HierarchyForProjectGuid(projectGuid);
                    fileStorage.SaveProject(project);
                }
                catch (Exception ex)
                {
                    string msg = $"Failed to save json for project '{projectGuid}' with error: {ex}";
                    Logger.Error(msg);
                }
            }
        }

        private void OnTreeChangedThrottled(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            var projectGuid = e.AffectedProject.Id;
            var project = vsHelper.HierarchyForProjectGuid(projectGuid);
            projectConfigService.UpdateConfigurationForProject(project);
        }

        private void OnTreeChanged(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            viewModelUpdateService.UpdateIsActiveForArgumentsDebounced();
        }
    }
}
