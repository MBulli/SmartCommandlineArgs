using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;

namespace SmartCmdArgs.Services
{
    internal interface ITreeViewEventHandlingService : IDisposable
    {
        void AttachToEvents();
        void DetachFromEvents();
    }

    internal class TreeViewEventHandlingService : ITreeViewEventHandlingService
    {
        private readonly TreeViewModel treeViewModel;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly IOptionsSettingsService optionsSettings;
        private readonly IFileStorageService fileStorage;
        private readonly IProjectConfigService projectConfigService;
        private readonly IViewModelUpdateService viewModelUpdateService;

        public TreeViewEventHandlingService(
            TreeViewModel treeViewModel,
            IVisualStudioHelperService vsHelper,
            IOptionsSettingsService optionsSettings,
            IFileStorageService fileStorage,
            IProjectConfigService projectConfigService,
            IViewModelUpdateService viewModelUpdateService)
        {
            this.treeViewModel = treeViewModel;
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
            treeViewModel.ItemSelectionChanged += OnItemSelectionChanged;
            treeViewModel.TreeContentChangedThrottled += OnTreeContentChangedThrottled;
            treeViewModel.TreeChangedThrottled += OnTreeChangedThrottled;
            treeViewModel.TreeChanged += OnTreeChanged;
        }

        public void DetachFromEvents()
        {
            treeViewModel.ItemSelectionChanged -= OnItemSelectionChanged;
            treeViewModel.TreeContentChangedThrottled -= OnTreeContentChangedThrottled;
            treeViewModel.TreeChangedThrottled -= OnTreeChangedThrottled;
            treeViewModel.TreeChanged -= OnTreeChanged;
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
            projectConfigService.UpdateProjectConfig(project);
        }

        private void OnTreeChanged(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            viewModelUpdateService.UpdateIsActiveForParamsDebounced();
        }
    }
}
