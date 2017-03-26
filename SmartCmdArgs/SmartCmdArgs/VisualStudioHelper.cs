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
    class VisualStudioHelper : IVsUpdateSolutionEvents2, IVsSelectionEvents, IVsSolutionEvents, IVsSolutionEvents4, IVsBuildStatusCallback
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
        private IVsBuildableProjectCfg currentBuildableProjectCfg;

        private bool initialized = false;
        private uint solutionEventsCookie = 0;
        private uint selectionEventsCookie = 0;
        private uint updateSolutionEventsCookie = 0;
        private uint buildStatusEventsCookie = 0;

        private CommandEvents commandEvents;
        private readonly string _VSConstants_VSStd97CmdID_GUID;

        public EnvDTE.Solution Solution { get { return appObject.Solution; } }
        public bool IsSolutionOpen { get { return appObject?.Solution?.IsOpen ?? false; } }

        public event EventHandler BuildProcessStarted;
        public event EventHandler ProjectBeforeRun;

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

        class ProjectState
        {
            public string FilePath;
            public bool IsLoaded;
        }

        private Dictionary<Guid, ProjectState> ProjectStateMap = new Dictionary<Guid, ProjectState>();

        public VisualStudioHelper(CmdArgsPackage package)
        {
            this.package = package;
            this.appObject = package.GetService<SDTE, EnvDTE.DTE>();
            _VSConstants_VSStd97CmdID_GUID = typeof(VSConstants.VSStd97CmdID).GUID.ToString("B").ToUpper();
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

                commandEvents = this.appObject.Events.CommandEvents;
                commandEvents.BeforeExecute += CommandEventsOnBeforeExecute;

                UpdateProjectBuildCallback(GetStartupProjectHierachy());

                if (IsSolutionOpen)
                {
                    foreach (var pHierarchy in GetSupportedProjects())
                    {
                        var project = ProjectForHierarchy(pHierarchy);
                        if (!ProjectArguments.IsSupportedProject(project))
                            continue;
                        
                        Guid projectGuid = pHierarchy.GetGuid();
                        string projectPath = project.FullName;
                        bool isLoaded = pHierarchy.IsLoaded();

                        ProjectStateMap[projectGuid] = new ProjectState{ FilePath = projectPath, IsLoaded = isLoaded };
                    }
                }

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
            if (buildStatusEventsCookie != 0)
                ErrorHandler.ThrowOnFailure(currentBuildableProjectCfg.UnadviseBuildStatusCallback(buildStatusEventsCookie));

            buildStatusEventsCookie = 0;
            selectionEventsCookie = 0;
            updateSolutionEventsCookie = 0;

            this.solutionService = null;
            this.solutionBuildService = null;
            this.selectionMonitor = null;
            this.currentBuildableProjectCfg = null;

            commandEvents.BeforeExecute -= CommandEventsOnBeforeExecute;

            initialized = false;
        }

        public string StartupProjectUniqueName()
        {
            var startupProjects = this.appObject?.Solution?.SolutionBuild?.StartupProjects as object[];
            return startupProjects?.FirstOrDefault() as string;
        }

        public IEnumerable<IVsHierarchy> GetSupportedProjects(bool includeUnloaded = false)
        {
            __VSENUMPROJFLAGS property = includeUnloaded ? __VSENUMPROJFLAGS.EPF_ALLINSOLUTION : __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION;

            Guid guid = Guid.Empty;
            solutionService.GetProjectEnum((uint)property, ref guid, out IEnumHierarchies enumerator);

            IVsHierarchy[] hierarchy = new IVsHierarchy[1] { null };
            uint fetched = 0;
            for (enumerator.Reset(); enumerator.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1; /*nothing*/)
            {
                if (ProjectArguments.IsSupportedProject(hierarchy[0]))
                {
                    yield return hierarchy[0];
                }
            }
        }

        public IEnumerable<string> GetSupportedProjectUniqueNames(bool includeUnloaded = false)
        {
            return GetSupportedProjects(includeUnloaded).Select(GetUniqueName);
        }
        
        public Project GetStartupProject()
        {
            return ProjectForHierarchy(GetStartupProjectHierachy());
        }

        public IVsHierarchy GetStartupProjectHierachy()
        {
            selectionMonitor.GetCurrentElementValue((uint) VSConstants.VSSELELEMID.SEID_StartupProject, out object value);
            return value as IVsHierarchy;
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

        public string GetUniqueName(IVsHierarchy hierarchy)
        {
            solutionService.GetUniqueNameOfProject(hierarchy, out string uniqueName);
            return uniqueName;
        }

        public string GetMSBuildPropertyValue(string projectName, string propName)
        {
            var hierarchy =  HierarchyForProjectName(projectName);
            var project = ProjectForHierarchy(hierarchy);
            var propStorage = hierarchy as IVsBuildPropertyStorage;

            if (propStorage == null)
                return null;

            string configName = null;
            try
            {
                configName = project.ConfigurationManager.ActiveConfiguration.ConfigurationName;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to get active configuration name for project '{projectName}' with error '{ex}'");
                return null;
            }

            if (configName != null)
            {
                if (ErrorHandler.Succeeded(propStorage.GetPropertyValue(propName, configName,
                                                                        (int)_PersistStorageType.PST_PROJECT_FILE,
                                                                        out string value)))
                {
                    return value;
                }
            }

            return null;
        }

        private void UpdateProjectBuildCallback(IVsHierarchy projectHierarchy)
        {
            if (currentBuildableProjectCfg != null)
            {
                // Unadvise
                ErrorHandler.ThrowOnFailure(currentBuildableProjectCfg.UnadviseBuildStatusCallback(buildStatusEventsCookie));
                currentBuildableProjectCfg = null;
                buildStatusEventsCookie = 0;
            }

            if (projectHierarchy != null)
            {
                // Advise
                IVsProjectCfg[] ppIVsProjectCfg = new IVsProjectCfg[1];
                ErrorHandler.ThrowOnFailure(solutionBuildService.FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, projectHierarchy, ppIVsProjectCfg));

                ppIVsProjectCfg[0].get_BuildableProjectCfg(out currentBuildableProjectCfg);
                ErrorHandler.ThrowOnFailure(currentBuildableProjectCfg.AdviseBuildStatusCallback(this, out buildStatusEventsCookie));
            }
        }

        private void OnStartupProjectChanged(IVsHierarchy startupProjectHierarchy)
        {
            UpdateProjectBuildCallback(startupProjectHierarchy);
            StartupProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CommandEventsOnBeforeExecute(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            if (guid == _VSConstants_VSStd97CmdID_GUID)
            {
                switch ((VSConstants.VSStd97CmdID)id)
                {
                    case VSConstants.VSStd97CmdID.Start:
                    case VSConstants.VSStd97CmdID.StartNoDebug:
                    case VSConstants.VSStd97CmdID.Restart:
                        ProjectBeforeRun?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
        }

        #region IVsSelectionEvents Implementation
        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            if (elementid == (uint)VSConstants.VSSELELEMID.SEID_StartupProject)
            {
                if (varValueNew != null)
                {
                    OnStartupProjectChanged((IVsHierarchy)varValueNew);
                }
            }
            return S_OK;
        }

        int IVsSelectionEvents.OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew) { return S_OK; }
        int IVsSelectionEvents.OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) { return S_OK; }
        #endregion

        #region IVsBuildStatusCallback Implementation
        int IVsBuildStatusCallback.BuildBegin(ref int pfContinue)
        {
            BuildProcessStarted?.Invoke(this, EventArgs.Empty);
            pfContinue = 1;
            return S_OK;
        }
        int IVsBuildStatusCallback.BuildEnd(int fSuccess) { return S_OK; }
        int IVsBuildStatusCallback.Tick(ref int pfContinue) { return S_OK; }
        #endregion

        #region IVsUpdateSolutionEvents2 Implementation
        int IVsUpdateSolutionEvents.OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            // This method is called if the user is changing solution or project configuration e.g. from Debug to Release.
            // Also if a new solution config was created.
            // This method is called for each Hierarchy element (e.g. project and folders), thus we filter for the startup project
            // to only trigger the config changed event once.

            if (GetUniqueName(pIVsHierarchy) == StartupProjectUniqueName())
            {
                UpdateProjectBuildCallback(pIVsHierarchy);
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
        int IVsSolutionEvents.OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            SolutionAfterOpen?.Invoke(this, EventArgs.Empty);
            return S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseSolution(object pUnkReserved)
        {
            SolutionBeforeClose?.Invoke(this, EventArgs.Empty);
            return S_OK;
        }

        int IVsSolutionEvents.OnAfterCloseSolution(object pUnkReserved)
        {
            ProjectStateMap.Clear();
            SolutionAfterClose?.Invoke(this, EventArgs.Empty);
            return S_OK;
        }

        int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            var project = ProjectForHierarchy(pHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();
            string projectPath = project.FullName;
            bool isLoaded = pHierarchy.IsLoaded();

            ProjectStateMap[projectGuid] =  new ProjectState{ FilePath = projectPath, IsLoaded = isLoaded };

            ProjectAfterOpen?.Invoke(this, project);

            return S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
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

        int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            var project = ProjectForHierarchy(pRealHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();
            
            ProjectStateMap[pRealHierarchy.GetGuid()].IsLoaded = true;

            ProjectAfterLoad?.Invoke(this, project);

            return S_OK;
        }

        int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            var project = ProjectForHierarchy(pRealHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            ProjectStateMap[pRealHierarchy.GetGuid()].IsLoaded = false;

            ProjectBeforeUnload?.Invoke(this, project);

            return S_OK;
        }


        int IVsSolutionEvents4.OnAfterRenameProject(IVsHierarchy pHierarchy)
        {
            var project = ProjectForHierarchy(pHierarchy);
            if (!ProjectArguments.IsSupportedProject(project))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();
            var oldName = ProjectStateMap[projectGuid].FilePath;
            ProjectStateMap[projectGuid].FilePath = project.FullName;

            ProjectAfterRename?.Invoke(this, new ProjectRenamedEventArgs { project = project, oldName = oldName });

            return S_OK;
        }

        public class ProjectRenamedEventArgs
        {
            public Project project;
            public string oldName;
        }

        #region unused
        int IVsSolutionEvents.OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) { return S_OK; }
        int IVsSolutionEvents.OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) { return S_OK; }
        int IVsSolutionEvents.OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) { return S_OK; }
        int IVsSolutionEvents4.OnQueryChangeProjectParent(IVsHierarchy pHierarchy, IVsHierarchy pNewParentHier, ref int pfCancel) { return S_OK; }
        int IVsSolutionEvents4.OnAfterChangeProjectParent(IVsHierarchy pHierarchy) { return S_OK; }
        int IVsSolutionEvents4.OnAfterAsynchOpenProject(IVsHierarchy pHierarchy, int fAdded) { return S_OK; }
        #endregion
        #endregion
        
        private int LogIgnoringUnsupportedProjectType([CallerMemberName] string eventName = null)
        {
            Logger.Info($"{eventName} event was ignored because project type is not supported.");
            return S_OK;
        }
    }
}
