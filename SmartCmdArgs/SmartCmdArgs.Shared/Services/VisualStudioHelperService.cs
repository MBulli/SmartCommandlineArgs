using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCmdArgs.Services
{
    public class ProjectAfterOpenEventArgs
    {
        public IVsHierarchy Project;
        public bool IsLoadProcess;
        public bool IsSolutionOpenProcess;
    }

    public class ProjectBeforeCloseEventArgs
    {
        public IVsHierarchy Project;
        public bool IsUnloadProcess;
        public bool IsSolutionCloseProcess;
    }

    public class ProjectAfterRenameEventArgs
    {
        public IVsHierarchy Project;
        public string OldProjectName;
        public string OldProjectDir;
    }

    interface IVisualStudioHelperService
    {
        bool IsSolutionOpen { get; }

        event EventHandler ProjectBeforeRun;

        event EventHandler StartupProjectChanged;
        event EventHandler<IVsHierarchy> ProjectConfigurationChanged;
        event EventHandler<IVsHierarchy> LaunchProfileChanged;

        event EventHandler SolutionAfterOpen;
        event EventHandler SolutionBeforeClose;
        event EventHandler SolutionAfterClose;

        event EventHandler<ProjectAfterOpenEventArgs> ProjectAfterOpen;
        event EventHandler<ProjectBeforeCloseEventArgs> ProjectBeforeClose;
        event EventHandler<IVsHierarchy> ProjectAfterLoad;
        event EventHandler<IVsHierarchy> ProjectBeforeUnload;
        event EventHandler<ProjectAfterRenameEventArgs> ProjectAfterRename;

        string GetSolutionFilename();
        IEnumerable<string> StartupProjectUniqueNames();
        void SetNewStartupProject(string projectName);
        IEnumerable<IVsHierarchy> GetSupportedProjects(bool includeUnloaded = false);
        void UpdateShellCommandUI(bool immediateUpdate = true);
        IVsHierarchy HierarchyForProjectName(string projectName);
        IVsHierarchy HierarchyForProjectGuid(Guid propjectGuid);
        Guid ProjectGuidForProjetName(string projectName);
        string GetUniqueName(IVsHierarchy hierarchy);
        string GetMSBuildPropertyValueForActiveConfig(IVsHierarchy hierarchy, string propName);
        string GetMSBuildPropertyValue(IVsHierarchy hierarchy, string propName, string configName = null);
        bool CanEditFile(string fileName);
        Task OpenFileInVisualStudioAsync(string path);
    }

    class VisualStudioHelperService : IVisualStudioHelperService, IAsyncInitializable, IVsUpdateSolutionEvents2, IVsSelectionEvents, IVsSolutionEvents, IVsSolutionEvents4
    {
        /// <summary>
        /// Shortcut for Microsoft.VisualStudio.VSConstants.S_OK
        /// </summary>
        private const int S_OK = VSConstants.S_OK;

        private CmdArgsPackage package;
        private EnvDTE.DTE appObject;

        private IVsSolution2 solutionService;
        private IVsSolutionBuildManager2 solutionBuildService;
        private IVsMonitorSelection selectionMonitor;

        private bool initialized = false;
        private uint solutionEventsCookie = 0;
        private uint selectionEventsCookie = 0;
        private uint updateSolutionEventsCookie = 0;

        private CommandEvents commandEvents;
        private readonly string _VSConstants_VSStd97CmdID_GUID;
        private readonly string _VSConstants_VSStd2KCmdID_GUID;

        public bool IsSolutionOpen { get { return appObject?.Solution?.IsOpen ?? false; } }

        public event EventHandler ProjectBeforeRun;

        public event EventHandler StartupProjectChanged;
        public event EventHandler<IVsHierarchy> ProjectConfigurationChanged;
        public event EventHandler<IVsHierarchy> LaunchProfileChanged;

        public event EventHandler SolutionAfterOpen;
        public event EventHandler SolutionBeforeClose;
        public event EventHandler SolutionAfterClose;

        public event EventHandler<ProjectAfterOpenEventArgs> ProjectAfterOpen;
        public event EventHandler<ProjectBeforeCloseEventArgs> ProjectBeforeClose;
        public event EventHandler<IVsHierarchy> ProjectAfterLoad;
        public event EventHandler<IVsHierarchy> ProjectBeforeUnload;
        public event EventHandler<ProjectAfterRenameEventArgs> ProjectAfterRename;

        private readonly IProjectConfigService _projectConfigService;

        class ProjectState : IDisposable
        {
            public string ProjectName;
            public string ProjectDir;
            public bool IsLoaded;

            private IDisposable _launchSettingsChangeListenerDisposable;

            public ProjectState(IVsHierarchy pHierarchy, Action<ILaunchSettings> launchProfileChangeAction)
            {
                ProjectDir = pHierarchy.GetProjectDir();
                ProjectName = pHierarchy.GetName();
                IsLoaded = pHierarchy.IsLoaded();

                if (pHierarchy.IsCpsProject())
                    _launchSettingsChangeListenerDisposable = CpsProjectSupport.ListenToLaunchProfileChanges(pHierarchy.GetProject(), launchProfileChangeAction);
            }

            public void Dispose()
            {
                _launchSettingsChangeListenerDisposable?.Dispose();
            }
        }

        private Dictionary<Guid, ProjectState> ProjectStateMap = new Dictionary<Guid, ProjectState>();

        public VisualStudioHelperService(IProjectConfigService projectConfigService)
        {
            this.package = CmdArgsPackage.Instance;
            _projectConfigService = projectConfigService;

            _VSConstants_VSStd97CmdID_GUID = typeof(VSConstants.VSStd97CmdID).GUID.ToString("B").ToUpper();
            _VSConstants_VSStd2KCmdID_GUID = typeof(VSConstants.VSStd2KCmdID).GUID.ToString("B").ToUpper();
        }

        public async Task InitializeAsync()
        {
            if (!initialized)
            {
                this.appObject = await package.GetServiceAsync<SDTE, DTE>();

                // Setup solution related stuff
                this.solutionService = await package.GetServiceAsync<SVsSolution, IVsSolution2>();
                this.solutionBuildService = await package.GetServiceAsync<SVsSolutionBuildManager, IVsSolutionBuildManager2>();
                this.selectionMonitor = await package.GetServiceAsync<SVsShellMonitorSelection, IVsMonitorSelection>();

                // Following code needs to be executed on main thread
                await package.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Set startup project
                ErrorHandler.ThrowOnFailure(this.solutionService.AdviseSolutionEvents(this, out solutionEventsCookie));
                ErrorHandler.ThrowOnFailure(this.selectionMonitor.AdviseSelectionEvents(this, out selectionEventsCookie));
                ErrorHandler.ThrowOnFailure(this.solutionBuildService.AdviseUpdateSolutionEvents(this, out updateSolutionEventsCookie));

                commandEvents = this.appObject.Events.CommandEvents;
                commandEvents.BeforeExecute += CommandEventsOnBeforeExecute;

                if (IsSolutionOpen)
                {
                    foreach (var pHierarchy in GetSupportedProjects())
                    {
                        AddProjectState(pHierarchy);
                    }
                }

                initialized = true;
            }
        }

        private void AddProjectState(IVsHierarchy pHierarchy)
        {
            Guid projectGuid = pHierarchy.GetGuid();
            ProjectStateMap.GetValueOrDefault(projectGuid)?.Dispose();
            ProjectStateMap[projectGuid] = new ProjectState(pHierarchy, profile =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    LaunchProfileChanged?.Invoke(this, HierarchyForProjectGuid(projectGuid));
                });
            });
        }

        public string GetSolutionFilename()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = solutionService.GetSolutionInfo(out string slnDir, out string slnFile, out string suoFile);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetSolutionInfo", hr);
            }

            if (slnDir == null || slnFile == null)
                return null;

            return System.IO.Path.Combine(slnDir, slnFile);
        }

        public IEnumerable<string> StartupProjectUniqueNames()
        {
            return (this.appObject?.Solution?.SolutionBuild?.StartupProjects as object[])?.Cast<string>() ?? Enumerable.Empty<string>();
        }

        public void SetNewStartupProject(string ProjectName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (this.appObject?.Solution?.SolutionBuild != null)
                this.appObject.Solution.SolutionBuild.StartupProjects = ProjectName;
        }

        public IEnumerable<IVsHierarchy> GetSupportedProjects(bool includeUnloaded = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            __VSENUMPROJFLAGS property = includeUnloaded ? __VSENUMPROJFLAGS.EPF_ALLINSOLUTION : __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION;

            Guid guid = Guid.Empty;
            solutionService.GetProjectEnum((uint)property, ref guid, out IEnumHierarchies enumerator);

            IVsHierarchy[] hierarchy = new IVsHierarchy[1] { null };
            uint fetched = 0;
            for (enumerator.Reset(); enumerator.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1; /*nothing*/)
            {
                if (_projectConfigService.IsSupportedProject(hierarchy[0]))
                {
                    yield return hierarchy[0];
                }
            }
        }

        public void UpdateShellCommandUI(bool immediateUpdate = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            package.GetService<SVsUIShell, IVsUIShell>()?.UpdateCommandUI(immediateUpdate ? 1 : 0);
        }

        public IVsHierarchy HierarchyForProjectName(string projectName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = solutionService.GetProjectOfUniqueName(projectName, out IVsHierarchy hier);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetProjectOfUniqueName", hr);
            }
            return hier;
        }

        public IVsHierarchy HierarchyForProjectGuid(Guid propjectGuid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = solutionService.GetProjectOfGuid(propjectGuid, out IVsHierarchy hier);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetProjectOfGuid", hr);
            }
            return hier;
        }

        public Guid ProjectGuidForProjetName(string projectName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var hier = HierarchyForProjectName(projectName);

            int hr = solutionService.GetGuidOfProject(hier, out Guid result);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetGuidOfProject", hr);
            }
            return result;
        }

        public string GetUniqueName(IVsHierarchy hierarchy)
        {
            if (hierarchy == null)
                return null;

            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = solutionService.GetUniqueNameOfProject(hierarchy, out string uniqueName);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetUniqueNameOfProject", hr);
            }
            return uniqueName;
        }

        public string GetMSBuildPropertyValueForActiveConfig(IVsHierarchy hierarchy, string propName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string configName = null;
            try
            {
                configName = hierarchy.GetProject()?.ConfigurationManager.ActiveConfiguration.ConfigurationName;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to get active configuration name for project '{hierarchy.GetName()}' with error '{ex}'");
                return null;
            }

            if (configName == null)
                return null;

            return GetMSBuildPropertyValue(hierarchy, propName, configName);
        }

        public string GetMSBuildPropertyValue(IVsHierarchy hierarchy, string propName, string configName = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var propStorage = hierarchy as IVsBuildPropertyStorage;

            if (propStorage == null)
                return null;

            // configName can be null
            if (ErrorHandler.Succeeded(propStorage.GetPropertyValue(propName, configName,
                                                                    (int)_PersistStorageType.PST_PROJECT_FILE,
                                                                    out string value)))
            {
                return value;
            }

            return null;
        }

        private bool gettingCheckoutStatus;

        /// <summary>
        /// This function asks to the QueryEditQuerySave service if it is possible to
        /// edit the file.
        /// </summary>
        /// <returns>True if the editing of the file are enabled, otherwise returns false.</returns>
        public bool CanEditFile(string fileName)
        {
            // see: https://github.com/Microsoft/VSSDK-Extensibility-Samples/blob/master/Editor_With_Toolbox/CS/EditorPane.cs

            // Check the status of the recursion guard
            if (gettingCheckoutStatus)
                return false;

            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // Set the recursion guard
                gettingCheckoutStatus = true;

                // Get the QueryEditQuerySave service
                IVsQueryEditQuerySave2 queryEditQuerySave = package.GetService<SVsQueryEditQuerySave, IVsQueryEditQuerySave2>();

                if (queryEditQuerySave == null)
                    return false;

                // Now call the QueryEdit method to find the edit status of this file
                string[] documents = { fileName };
                uint result;
                uint outFlags;

                // Note that this function can pop up a dialog to ask the user to checkout the file.
                // When this dialog is visible, it is possible to receive other request to change
                // the file and this is the reason for the recursion guard.
                int hr = queryEditQuerySave.QueryEditFiles(
                    0,              // Flags
                    1,              // Number of elements in the array
                    documents,      // Files to edit
                    null,           // Input flags
                    null,           // Input array of VSQEQS_FILE_ATTRIBUTE_DATA
                    out result,     // result of the checkout
                    out outFlags    // Additional flags
                );

                if (ErrorHandler.Succeeded(hr) && (result == (uint)tagVSQueryEditResult.QER_EditOK))
                {
                    // In this case (and only in this case) we can return true from this function.
                    return true;
                }
                else
                {
                    Logger.Error($"QueryEditFiles() returned non-ok result ({result})");
                    return false;
                }
            }
            finally
            {
                gettingCheckoutStatus = false;
            }
        }

        public async Task OpenFileInVisualStudioAsync(string path)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            appObject.ItemOperations.OpenFile(path);
        }


        Timer _startupProjectCheckTimer = null;
        private void OnStartupProjectChanged(IVsHierarchy startupProjectHierarchy)
        {
            StartupProjectChanged?.Invoke(this, EventArgs.Empty);

            var curStartUpProjects = StartupProjectUniqueNames().ToList();

            SynchronizationContext context = SynchronizationContext.Current;

            _startupProjectCheckTimer?.Dispose();
            _startupProjectCheckTimer = new Timer(
                (ignore) =>
                {
                    _startupProjectCheckTimer?.Dispose();
                    context.Post(ignore2 =>
                    {
                        var newStartUpProjects = StartupProjectUniqueNames().ToList();
                        if (newStartUpProjects.Count != curStartUpProjects.Count || newStartUpProjects.Zip(curStartUpProjects, (s1, s2) => s1 != s2).Any(b => b))
                            StartupProjectChanged?.Invoke(this, EventArgs.Empty);
                    }, null);

                }, null, 500, Timeout.Infinite);
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
            else if (guid == _VSConstants_VSStd2KCmdID_GUID)
            {
                switch ((VSConstants.VSStd2KCmdID)id)
                {
                    case VSConstants.VSStd2KCmdID.PROJSTARTDEBUG:
                    case VSConstants.VSStd2KCmdID.PROJSTEPINTO:
                        ProjectBeforeRun?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
        }

        #region IVsSelectionEvents Implementation
        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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

        #region IVsUpdateSolutionEvents2 Implementation
        int IVsUpdateSolutionEvents.OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            // This method is called if the user is changing solution or project configuration e.g. from Debug to Release.
            // Also if a new solution config was created.
            // This method is called for each Hierarchy element (e.g. project and folders), thus we filter for the startup project
            // to only trigger the config changed event once.

            ProjectConfigurationChanged?.Invoke(this, pIVsHierarchy);

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
            ProjectStateMap.ForEach(x => x.Value.Dispose());
            ProjectStateMap.Clear();
            SolutionAfterClose?.Invoke(this, EventArgs.Empty);
            return S_OK;
        }

        int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            if (!_projectConfigService.IsSupportedProject(pHierarchy))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();

            bool isLoadProcess = ProjectStateMap.TryGetValue(projectGuid, out var state) && state.IsLoaded;
            AddProjectState(pHierarchy);

            ProjectAfterOpen?.Invoke(this, new ProjectAfterOpenEventArgs { Project = pHierarchy, IsLoadProcess = isLoadProcess, IsSolutionOpenProcess = fAdded == 0 });

            return S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            if (!_projectConfigService.IsSupportedProject(pHierarchy))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();

            var isUloadProcess = ProjectStateMap.TryGetValue(projectGuid, out var state) && !state.IsLoaded;

            if (!isUloadProcess)
            {
                ProjectStateMap.GetValueOrDefault(projectGuid)?.Dispose();
                ProjectStateMap.Remove(projectGuid);
            }

            ProjectBeforeClose?.Invoke(this, new ProjectBeforeCloseEventArgs { Project = pHierarchy, IsUnloadProcess = isUloadProcess, IsSolutionCloseProcess = fRemoved == 0 });

            return S_OK;
        }

        int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            if (!_projectConfigService.IsSupportedProject(pRealHierarchy))
                return LogIgnoringUnsupportedProjectType();

            ProjectStateMap[pRealHierarchy.GetGuid()].IsLoaded = true;

            ProjectAfterLoad?.Invoke(this, pRealHierarchy);

            return S_OK;
        }

        int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            if (!_projectConfigService.IsSupportedProject(pRealHierarchy))
                return LogIgnoringUnsupportedProjectType();

            ProjectStateMap[pRealHierarchy.GetGuid()].IsLoaded = false;

            ProjectBeforeUnload?.Invoke(this, pRealHierarchy);

            return S_OK;
        }


        int IVsSolutionEvents4.OnAfterRenameProject(IVsHierarchy pHierarchy)
        {
            if (!_projectConfigService.IsSupportedProject(pHierarchy))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = pHierarchy.GetGuid();
            var oldProjectDir = ProjectStateMap[projectGuid].ProjectDir;
            var oldProjectName = ProjectStateMap[projectGuid].ProjectName;
            ProjectStateMap[projectGuid].ProjectDir = pHierarchy.GetProjectDir();
            ProjectStateMap[projectGuid].ProjectName = pHierarchy.GetName();

            ProjectAfterRename?.Invoke(this, new ProjectAfterRenameEventArgs { OldProjectDir = oldProjectDir, OldProjectName = oldProjectName, Project = pHierarchy });

            return S_OK;
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

    class VisualStudioHelperException : Exception
    {
        public VisualStudioHelperException(string calledFunction, int hr, [CallerMemberName] string callingFunction = null)
            : base($"Call to {calledFunction} in {callingFunction} failed with error: {Marshal.GetExceptionForHR(hr).Message}", Marshal.GetExceptionForHR(hr))
        {

        }
    }
}
