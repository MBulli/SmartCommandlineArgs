using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using SmartCmdArgs.Helper;

namespace SmartCmdArgs
{
    class VisualStudioHelper : IVsUpdateSolutionEvents2, IVsSelectionEvents, IVsSolutionEvents, IVsSolutionEvents4
    {
        /// <summary>
        /// Shortcut for Microsoft.VisualStudio.VSConstants.S_OK
        /// </summary>
        private const int S_OK = Microsoft.VisualStudio.VSConstants.S_OK;

        private CmdArgsPackage package;
        private EnvDTE.DTE appObject;

        private IVsSolution2 solutionService;
        private IVsSolutionBuildManager2 solutionBuildService;
        private IVsMonitorSelection selectionMonitor;

        private bool initialized = false;
        private uint solutionEventsCookie = 0;
        private uint selectionEventsCookie = 0;
        private uint updateSolutionEventsCookie = 0;

        public EnvDTE.Solution Solution { get { return appObject.Solution; } }
        public bool IsSolutionOpen { get { return appObject?.Solution?.IsOpen ?? false; } }

        public event EventHandler StartupProjectChanged;
        public event EventHandler StartupProjectConfigurationChanged;

        public event EventHandler SolutionAfterOpen;
        public event EventHandler SolutionBeforeClose;
        public event EventHandler SolutionAfterClose;

        public event EventHandler<Project> ProjectAfterOpen;
        public event EventHandler<Project> ProjectBeforeClose;
        public event EventHandler<Project> ProjectAfterLoad;
        public event EventHandler<Project> ProjectBeforeUnload;
        public event EventHandler<ProjectRenamedEventArgs> ProjectAfterRename;

        private Dictionary<Guid, (string FilePath, bool IsLoaded)> ProjectStateMap = new Dictionary<Guid, ValueTuple<string, bool>>();

        public VisualStudioHelper(CmdArgsPackage package)
        {
            this.package = package;
            this.appObject = package.GetService<SDTE, EnvDTE.DTE>();
        }

        public void Initialize()
        {
            if (!initialized)
            {
                // Setup solution related stuff
                this.solutionService = package.GetService<SVsSolution, IVsSolution2>();
                this.solutionBuildService = package.GetService<SVsSolutionBuildManager, IVsSolutionBuildManager2>();
                this.selectionMonitor = package.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();

                // Set startup project
                ErrorHandler.ThrowOnFailure(this.solutionService.AdviseSolutionEvents(this, out solutionEventsCookie));
                ErrorHandler.ThrowOnFailure(this.selectionMonitor.AdviseSelectionEvents(this, out selectionEventsCookie));
                ErrorHandler.ThrowOnFailure(this.solutionBuildService.AdviseUpdateSolutionEvents(this, out updateSolutionEventsCookie));

                initialized = true;
            }
        }

        public void Deinitalize()
        {
            // Cleanup solution related stuff
            if (selectionEventsCookie != 0)
                ErrorHandler.ThrowOnFailure(this.selectionMonitor.UnadviseSelectionEvents(selectionEventsCookie));
            if (updateSolutionEventsCookie != 0)
                ErrorHandler.ThrowOnFailure(this.solutionBuildService.UnadviseUpdateSolutionEvents(updateSolutionEventsCookie));

            selectionEventsCookie = 0;
            updateSolutionEventsCookie = 0;

            this.solutionService = null;
            this.solutionBuildService = null;
            this.selectionMonitor = null;

            initialized = false;
        }

        public string StartupProjectUniqueName()
        {
            var startupProjects = this.appObject?.Solution?.SolutionBuild?.StartupProjects as object[];
            return startupProjects?.FirstOrDefault() as string;
        }

        public void GetProjects(EnvDTE.Project project, ref List<EnvDTE.Project> allProjects)
        {
            // Make sure we have a valid list
            if (allProjects == null)
            {
                allProjects = new List<EnvDTE.Project>();
            }

            // We determine if this is an actual project by looking if it has a ConfigurationManager
            // This could be wrong for some types of project, but it works for our needs
            if (ProjectArguments.IsSupportedProject(project))
            {
                allProjects.Add(project);
            }
            else if (project.ProjectItems != null)
            {
                foreach (EnvDTE.ProjectItem item in project.ProjectItems)
                {
                    if (item.SubProject != null && item.SubProject != project)
                    {
                        GetProjects(item.SubProject, ref allProjects);
                    }
                }
            }
        }

        public IEnumerable<EnvDTE.Project> FindAllProjects()
        {
            List<EnvDTE.Project> allProjects = new List<EnvDTE.Project>();
            if (this.appObject?.Solution != null)
            {
                foreach (EnvDTE.Project project in this.appObject?.Solution.Projects)
                {
                    GetProjects(project, ref allProjects);
                }
            }
            return allProjects;
        }

        public bool FindStartupProject(out EnvDTE.Project startupProject)
        {
            startupProject = null;

            string prjName = StartupProjectUniqueName();

            if (prjName != null)
            {
                try
                {
                    var project = this.appObject?.Solution.Item(prjName);
                    if (ProjectArguments.IsSupportedProject(project))
                        startupProject = project;
                }
                catch
                {
                    // If we couldn't find it in the solution directly, check in the nested projects
                    startupProject = FindAllProjects().FirstOrDefault(p => p.UniqueName == prjName);
                }
                return startupProject != null;
            }

            return false;
        }

        public void UpdateShellCommandUI(bool immediateUpdate = true)
        {
            package.GetService<SVsUIShell, IVsUIShell>()?.UpdateCommandUI(immediateUpdate ? 1 : 0);
        }

        public IVsHierarchy HierarchyForProject(Project project)
        {
            ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(project.UniqueName, out IVsHierarchy hier));
            return hier;
        }

        public IVsHierarchy HierarchyForProjectName(string projectName)
        {
            ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(projectName, out IVsHierarchy hier));
            return hier;
        }

        public Project ProjectForHierarchy(IVsHierarchy hierarchy)
        {
            return hierarchy?.GetExtObject() as EnvDTE.Project;
        }

        public Project ProjectForProjectName(string projectName)
        {
            return ProjectForHierarchy(HierarchyForProjectName(projectName));
        }

        private void SetProjectLoadState(IVsHierarchy hierarchy, bool isLoaded)
        {
            Guid projectGuid = hierarchy.GetGuid();
            var state = ProjectStateMap[projectGuid];
            state.IsLoaded = isLoaded;
            ProjectStateMap[projectGuid] = state;
        }

        private void SetProjectFilePath(IVsHierarchy hierarchy, string filePath)
        {
            Guid projectGuid = hierarchy.GetGuid();
            var state = ProjectStateMap[projectGuid];
            state.FilePath = filePath;
            ProjectStateMap[projectGuid] = state;
        }

        public string GetMSBuildPropertyValue(string projectName, string propName)
        {
            var hierarchy =  HierarchyForProjectName(projectName);
            var project = ProjectForHierarchy(hierarchy);
            var propStorage = (IVsBuildPropertyStorage)hierarchy;

            string configName = null;
            try
            {
                configName = project.ConfigurationManager.ActiveConfiguration.ConfigurationName;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to get active configuration name for project '{projectName}' with error '{ex}'");
            }

            if (configName != null)
            {
                string value;

                try
                {
                    ErrorHandler.ThrowOnFailure(propStorage.GetPropertyValue(propName, configName,
                        (int)_PersistStorageType.PST_PROJECT_FILE, out value));

                    return value;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to evaluate property '{propName}' for project '{projectName}' with configuration '{configName}' with error '{ex}'");
                }
            }
            return null;
        }

        #region IVsSelectionEvents Implementation
        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            if (elementid == (uint)VSConstants.VSSELELEMID.SEID_StartupProject)
            {
                if (varValueNew != null)
                {
                    StartupProjectChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            return S_OK;
        }

        int IVsSelectionEvents.OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew) { return S_OK; }
        int IVsSelectionEvents.OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) { return S_OK; }
        #endregion

        #region IVsUpdateSolutionEvents2 Implementation
        int IVsUpdateSolutionEvents.OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            // This method is called if the user is changing solution or project configuration e.g. from Debug to Release.
            // Also if a new solution config was created.
            // This method is called for each Hierarchy element (e.g. project and folders), thus we filter for the startup project
            // to only trigger the config changed event once.

            var project = ProjectForHierarchy(pIVsHierarchy);

            if (project?.UniqueName == StartupProjectUniqueName())
            {
                StartupProjectConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }

            return S_OK;
        }

        #region unused
        int IVsUpdateSolutionEvents2.UpdateSolution_Begin(ref int pfCancelUpdate) { return S_OK; }
        int IVsUpdateSolutionEvents2.UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand) { return S_OK; }
        int IVsUpdateSolutionEvents2.UpdateSolution_StartUpdate(ref int pfCancelUpdate) { return S_OK; }
        int IVsUpdateSolutionEvents2.UpdateSolution_Cancel() { return S_OK; }
        int IVsUpdateSolutionEvents2.OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy) { return S_OK; }
        int IVsUpdateSolutionEvents2.UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel) { return S_OK; }
        int IVsUpdateSolutionEvents2.UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel) { return S_OK; }
        int IVsUpdateSolutionEvents.UpdateSolution_Begin(ref int pfCancelUpdate) { return S_OK; }
        int IVsUpdateSolutionEvents.UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand) { return S_OK; }
        int IVsUpdateSolutionEvents.UpdateSolution_StartUpdate(ref int pfCancelUpdate) { return S_OK; }
        int IVsUpdateSolutionEvents.UpdateSolution_Cancel() { return S_OK; }
        #endregion
        #endregion

        #region IVsSolutionEvents Implementation
        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            SolutionAfterOpen?.Invoke(this, EventArgs.Empty);
            return S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            SolutionBeforeClose?.Invoke(this, EventArgs.Empty);
            return S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            ProjectStateMap.Clear();
            SolutionAfterClose?.Invoke(this, EventArgs.Empty);
            return S_OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            var project = ProjectForHierarchy(pHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();
            string projectPath = project.FullName;
            bool isLoaded = pHierarchy.IsLoaded();

            ProjectStateMap[projectGuid] = (projectPath, isLoaded);

            ProjectAfterOpen?.Invoke(this, project);

            return S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            var project = ProjectForHierarchy(pHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();

            if (!ProjectStateMap.TryGetValue(projectGuid, out var state) || !state.IsLoaded)
            {
                Logger.Info("OnBeforeCloseProject event was ignored because project is not open or not loaded.");
                return S_OK;
            }

            ProjectStateMap.Remove(projectGuid);
            
            ProjectBeforeClose?.Invoke(this, project);

            return S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            var project = ProjectForHierarchy(pRealHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            SetProjectLoadState(pRealHierarchy, isLoaded: true);
            
            ProjectAfterLoad?.Invoke(this, project);

            return S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            var project = ProjectForHierarchy(pRealHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            SetProjectLoadState(pRealHierarchy, isLoaded:false);
            
            ProjectBeforeUnload?.Invoke(this, project);

            return S_OK;
        }


        public int OnAfterRenameProject(IVsHierarchy pHierarchy)
        {
            var project = ProjectForHierarchy(pHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();
            var oldName = ProjectStateMap[projectGuid].FilePath;
            SetProjectFilePath(pHierarchy, project.FullName);

            ProjectAfterRename?.Invoke(this, new ProjectRenamedEventArgs { project = project, oldName = oldName });

            return S_OK;
        }

        public class ProjectRenamedEventArgs
        {
            public Project project;
            public string oldName;
        }

        #region unused
        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) { return S_OK; }
        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) { return S_OK; }
        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) { return S_OK; }
        public int OnQueryChangeProjectParent(IVsHierarchy pHierarchy, IVsHierarchy pNewParentHier, ref int pfCancel) { return S_OK; }
        public int OnAfterChangeProjectParent(IVsHierarchy pHierarchy) { return S_OK; }
        public int OnAfterAsynchOpenProject(IVsHierarchy pHierarchy, int fAdded) { return S_OK; }
        #endregion
        #endregion
        
        private int LogIgnoringUnsupportedProjectType([CallerMemberName] string eventName = null)
        {
            Logger.Info($"{eventName} event was ignored because project type is not supported.");
            return S_OK;
        }
    }
}
