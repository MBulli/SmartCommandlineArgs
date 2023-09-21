using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartCmdArgs.Services
{
    internal interface IVsEventHandlingService : IDisposable
    {
        void AttachToProjectEvents();
        void AttachToSolutionEvents();
        void DetachFromProjectEvents();
        void DetachFromSolutionEvents();
    }

    internal class VsEventHandlingService : IDisposable, IVsEventHandlingService
    {
        private readonly IVisualStudioHelperService vsHelper;
        private readonly ILifeCycleService lifeCycleService;
        private readonly IFileStorageService fileStorage;
        private readonly IViewModelUpdateService viewModelUpdateService;
        private readonly ToolWindowViewModel toolWindowViewModel;
        private readonly IProjectConfigService projectConfigService;

        public VsEventHandlingService(
            IVisualStudioHelperService vsHelper,
            ILifeCycleService lifeCycleService,
            IFileStorageService fileStorage,
            IViewModelUpdateService viewModelUpdateService,
            ToolWindowViewModel toolWindowViewModel,
            IProjectConfigService projectConfigService)
        {
            this.vsHelper = vsHelper;
            this.lifeCycleService = lifeCycleService;
            this.fileStorage = fileStorage;
            this.viewModelUpdateService = viewModelUpdateService;
            this.toolWindowViewModel = toolWindowViewModel;
            this.projectConfigService = projectConfigService;
        }

        public void Dispose()
        {
            DetachFromProjectEvents();
            DetachFromSolutionEvents();
        }

        public void AttachToSolutionEvents()
        {
            vsHelper.SolutionAfterOpen += VsHelper_SolutionOpend;
            vsHelper.SolutionBeforeClose += VsHelper_SolutionWillClose;
            vsHelper.SolutionAfterClose += VsHelper_SolutionClosed;
        }

        public void AttachToProjectEvents()
        {
            vsHelper.StartupProjectChanged += VsHelper_StartupProjectChanged;
            vsHelper.ProjectConfigurationChanged += VsHelper_ProjectConfigurationChanged;
            vsHelper.ProjectBeforeRun += VsHelper_ProjectWillRun;
            vsHelper.LaunchProfileChanged += VsHelper_LaunchProfileChanged;

            vsHelper.ProjectAfterOpen += VsHelper_ProjectAdded;
            vsHelper.ProjectBeforeClose += VsHelper_ProjectRemoved;
            vsHelper.ProjectAfterRename += VsHelper_ProjectRenamed;
            vsHelper.ProjectAfterLoad += VsHelper_ProjectAfterLoad;
        }

        public void DetachFromProjectEvents()
        {
            vsHelper.StartupProjectChanged -= VsHelper_StartupProjectChanged;
            vsHelper.ProjectConfigurationChanged -= VsHelper_ProjectConfigurationChanged;
            vsHelper.ProjectBeforeRun -= VsHelper_ProjectWillRun;
            vsHelper.LaunchProfileChanged -= VsHelper_LaunchProfileChanged;

            vsHelper.ProjectAfterOpen -= VsHelper_ProjectAdded;
            vsHelper.ProjectBeforeClose -= VsHelper_ProjectRemoved;
            vsHelper.ProjectAfterRename -= VsHelper_ProjectRenamed;
            vsHelper.ProjectAfterLoad -= VsHelper_ProjectAfterLoad;
        }

        public void DetachFromSolutionEvents()
        {
            vsHelper.SolutionAfterOpen -= VsHelper_SolutionOpend;
            vsHelper.SolutionBeforeClose -= VsHelper_SolutionWillClose;
            vsHelper.SolutionAfterClose -= VsHelper_SolutionClosed;
        }

        private void VsHelper_SolutionOpend(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution opened.");

            lifeCycleService.UpdateDisabledScreen();
            lifeCycleService.InitializeConfigForSolution();
        }

        private void VsHelper_SolutionWillClose(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution will close.");

            fileStorage.RemoveAllProjects();
        }

        private void VsHelper_SolutionClosed(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution closed.");

            lifeCycleService.FinalizeConfigForSolution();
        }

        private void VsHelper_StartupProjectChanged(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: startup project changed.");

            viewModelUpdateService.UpdateCurrentStartupProject();
        }

        private void VsHelper_ProjectConfigurationChanged(object sender, IVsHierarchy vsHierarchy)
        {
            Logger.Info("VS-Event: Project configuration changed.");

            viewModelUpdateService.UpdateIsActiveForArgumentsDebounced();
        }

        private void VsHelper_LaunchProfileChanged(object sender, IVsHierarchy e)
        {
            Logger.Info("VS-Event: Project launch profile changed.");

            viewModelUpdateService.UpdateIsActiveForArgumentsDebounced();
        }

        private void VsHelper_ProjectWillRun(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Startup project will run.");

            foreach (var startupProject in toolWindowViewModel.TreeViewModel.StartupProjects)
            {
                var project = vsHelper.HierarchyForProjectGuid(startupProject.Id);
                projectConfigService.UpdateConfigurationForProject(project);
                fileStorage.SaveProject(project);
            }
        }

        private void VsHelper_ProjectAdded(object sender, ProjectAfterOpenEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' added. (IsLoadProcess={e.IsLoadProcess}, IsSolutionOpenProcess={e.IsSolutionOpenProcess})");

            if (e.IsSolutionOpenProcess)
                return;

            ToolWindowHistory.SaveState();

            viewModelUpdateService.UpdateCommandsForProject(e.Project);
            fileStorage.AddProject(e.Project);
        }

        private void VsHelper_ProjectRemoved(object sender, ProjectBeforeCloseEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' removed. (IsUnloadProcess={e.IsUnloadProcess}, IsSolutionCloseProcess={e.IsSolutionCloseProcess})");

            if (e.IsSolutionCloseProcess)
                return;

            fileStorage.SaveProject(e.Project);

            toolWindowViewModel.TreeViewModel.Projects.Remove(e.Project.GetGuid());

            fileStorage.RemoveProject(e.Project);
        }

        private void VsHelper_ProjectRenamed(object sender, ProjectAfterRenameEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.OldProjectName}' renamed to '{e.Project.GetName()}'.");

            fileStorage.RenameProject(e.Project, e.OldProjectDir, e.OldProjectName);

            toolWindowViewModel.RenameProject(e.Project);
        }

        private void VsHelper_ProjectAfterLoad(object sender, IVsHierarchy e)
        {
            Logger.Info("VS-Event: Project loaded.");

            // Startup project must be set here beacuase in the event of a project
            // reload the StartupProjectChanged event is fired before the project
            // is added so we don't know it and can't set it as startup project
            viewModelUpdateService.UpdateCurrentStartupProject();
        }
    }
}
