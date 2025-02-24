using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Task = System.Threading.Tasks.Task;

namespace SmartCmdArgs.Services
{
    public class ProjectAfterOpenEventArgs
    {
        public IVsHierarchyWrapper Project;
        public bool IsLoadProcess;
        public bool IsSolutionOpenProcess;
    }

    public class ProjectBeforeCloseEventArgs
    {
        public IVsHierarchyWrapper Project;
        public bool IsUnloadProcess;
        public bool IsSolutionCloseProcess;
    }

    public class ProjectAfterRenameEventArgs
    {
        public IVsHierarchyWrapper Project;
        public string OldProjectName;
        public string OldProjectDir;
    }

    public interface IVisualStudioHelperService
    {
        bool IsSolutionOpen { get; }

        event EventHandler ProjectBeforeRun;

        event EventHandler StartupProjectChanged;
        event EventHandler<IVsHierarchyWrapper> ProjectConfigurationChanged;
        event EventHandler<IVsHierarchyWrapper> LaunchProfileChanged;

        event EventHandler SolutionAfterOpen;
        event EventHandler SolutionBeforeClose;
        event EventHandler SolutionAfterClose;

        event EventHandler<ProjectAfterOpenEventArgs> ProjectAfterOpen;
        event EventHandler<ProjectBeforeCloseEventArgs> ProjectBeforeClose;
        event EventHandler<IVsHierarchyWrapper> ProjectAfterLoad;
        event EventHandler<IVsHierarchyWrapper> ProjectBeforeUnload;
        event EventHandler<ProjectAfterRenameEventArgs> ProjectAfterRename;

        string GetSolutionFilename();
        IEnumerable<string> StartupProjectUniqueNames();
        IEnumerable<IVsHierarchyWrapper> GetStartupProjects();
        void SetNewStartupProject(string projectName);
        void SetAsStartupProject(Guid propjectGuid);
        IEnumerable<IVsHierarchyWrapper> GetSupportedProjects(bool includeUnloaded = false);
        void UpdateShellCommandUI(bool immediateUpdate = true);
        IVsHierarchyWrapper HierarchyForProjectName(string projectName);
        IVsHierarchyWrapper HierarchyForProjectGuid(Guid propjectGuid);
        string GetUniqueName(IVsHierarchyWrapper hierarchy);
        string GetMSBuildPropertyValueForActiveConfig(IVsHierarchyWrapper hierarchy, string propName);
        string GetMSBuildPropertyValue(IVsHierarchyWrapper hierarchy, string propName, string configName = null);
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
        public event EventHandler<IVsHierarchyWrapper> ProjectConfigurationChanged;
        public event EventHandler<IVsHierarchyWrapper> LaunchProfileChanged;

        public event EventHandler SolutionAfterOpen;
        public event EventHandler SolutionBeforeClose;
        public event EventHandler SolutionAfterClose;

        public event EventHandler<ProjectAfterOpenEventArgs> ProjectAfterOpen;
        public event EventHandler<ProjectBeforeCloseEventArgs> ProjectBeforeClose;
        public event EventHandler<IVsHierarchyWrapper> ProjectAfterLoad;
        public event EventHandler<IVsHierarchyWrapper> ProjectBeforeUnload;
        public event EventHandler<ProjectAfterRenameEventArgs> ProjectAfterRename;

        private readonly Lazy<IProjectConfigService> _projectConfigService;

        class ProjectState : IDisposable
        {
            public string ProjectName;
            public string ProjectDir;
            public bool IsLoaded;

            private IDisposable _launchSettingsChangeListenerDisposable;

            public ProjectState(IVsHierarchyWrapper pHierarchy, Action launchProfileChangeAction)
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

        public VisualStudioHelperService(Lazy<IProjectConfigService> projectConfigService)
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

        private void AddProjectState(IVsHierarchyWrapper pHierarchy)
        {
            Guid projectGuid = pHierarchy.GetGuid();
            ProjectStateMap.GetValueOrDefault(projectGuid)?.Dispose();
            ProjectStateMap[projectGuid] = new ProjectState(pHierarchy, () =>
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

        public IEnumerable<IVsHierarchyWrapper> GetStartupProjects()
        {
            return StartupProjectUniqueNames().Select(HierarchyForProjectName);
        }

        public void SetNewStartupProject(string ProjectName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (this.appObject?.Solution?.SolutionBuild != null)
                this.appObject.Solution.SolutionBuild.StartupProjects = ProjectName;
        }

        public void SetAsStartupProject(Guid guid)
        {
            SetNewStartupProject(GetUniqueName(HierarchyForProjectGuid(guid)));
        }

        public IEnumerable<IVsHierarchyWrapper> GetSupportedProjects(bool includeUnloaded = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            __VSENUMPROJFLAGS property = includeUnloaded ? __VSENUMPROJFLAGS.EPF_ALLINSOLUTION : __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION;

            Guid guid = Guid.Empty;
            solutionService.GetProjectEnum((uint)property, ref guid, out IEnumHierarchies enumerator);

            IVsHierarchy[] hierarchy = new IVsHierarchy[1] { null };
            uint fetched = 0;
            for (enumerator.Reset(); enumerator.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1; /*nothing*/)
            {
                var hierarchyWrapper = hierarchy[0].Wrap();
                if (_projectConfigService.Value.IsSupportedProject(hierarchyWrapper))
                {
                    yield return hierarchyWrapper;
                }
            }
        }

        public void UpdateShellCommandUI(bool immediateUpdate = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            package.GetService<SVsUIShell, IVsUIShell>()?.UpdateCommandUI(immediateUpdate ? 1 : 0);
        }

        public IVsHierarchyWrapper HierarchyForProjectName(string projectName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = solutionService.GetProjectOfUniqueName(projectName, out IVsHierarchy hier);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetProjectOfUniqueName", hr);
            }
            return hier.Wrap();
        }

        public IVsHierarchyWrapper HierarchyForProjectGuid(Guid propjectGuid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = solutionService.GetProjectOfGuid(propjectGuid, out IVsHierarchy hier);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetProjectOfGuid", hr);
            }
            return hier.Wrap();
        }

        public string GetUniqueName(IVsHierarchyWrapper hierarchyWrapper)
        {
            if (hierarchyWrapper == null)
                return null;

            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = solutionService.GetUniqueNameOfProject(hierarchyWrapper.Hierarchy, out string uniqueName);
            if (ErrorHandler.Failed(hr))
            {
                throw new VisualStudioHelperException("GetUniqueNameOfProject", hr);
            }
            return uniqueName;
        }

        public string GetMSBuildPropertyValueForActiveConfig(IVsHierarchyWrapper hierarchy, string propName)
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

        public string GetMSBuildPropertyValue(IVsHierarchyWrapper hierarchy, string propName, string configName = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var propStorage = hierarchy.Hierarchy as IVsBuildPropertyStorage;

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

        private CancellationTokenSource startupProjectChangedCTS = null;
        private void OnStartupProjectChanged(IVsHierarchyWrapper startupProjectHierarchy)
        {
            StartupProjectChanged?.Invoke(this, EventArgs.Empty);

            var curStartUpProjects = StartupProjectUniqueNames().ToList();

            startupProjectChangedCTS?.Cancel();
            startupProjectChangedCTS = new CancellationTokenSource();

            DelayExecution.ExecuteAfter(TimeSpan.FromMilliseconds(500), startupProjectChangedCTS.Token, () =>
            {
                startupProjectChangedCTS = null;
                var newStartUpProjects = StartupProjectUniqueNames().ToList();
                if (newStartUpProjects.Count != curStartUpProjects.Count || newStartUpProjects.Zip(curStartUpProjects, (s1, s2) => s1 != s2).Any(b => b))
                    StartupProjectChanged?.Invoke(this, EventArgs.Empty);
            });
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
                    case VSConstants.VSStd97CmdID.StepInto:
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
                    OnStartupProjectChanged(((IVsHierarchy)varValueNew).Wrap());
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

            ProjectConfigurationChanged?.Invoke(this, pIVsHierarchy.Wrap());

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
            var hierarchyWrapper = pHierarchy.Wrap();
            if (!_projectConfigService.Value.IsSupportedProject(hierarchyWrapper))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = hierarchyWrapper.GetGuid();

            bool isLoadProcess = ProjectStateMap.TryGetValue(projectGuid, out var state) && state.IsLoaded;
            AddProjectState(hierarchyWrapper);

            ProjectAfterOpen?.Invoke(this, new ProjectAfterOpenEventArgs { Project = hierarchyWrapper, IsLoadProcess = isLoadProcess, IsSolutionOpenProcess = fAdded == 0 });

            return S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            var hierarchyWrapper = pHierarchy.Wrap();
            if (!_projectConfigService.Value.IsSupportedProject(hierarchyWrapper))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = hierarchyWrapper.GetGuid();

            var isUloadProcess = ProjectStateMap.TryGetValue(projectGuid, out var state) && !state.IsLoaded;

            if (!isUloadProcess)
            {
                ProjectStateMap.GetValueOrDefault(projectGuid)?.Dispose();
                ProjectStateMap.Remove(projectGuid);
            }

            ProjectBeforeClose?.Invoke(this, new ProjectBeforeCloseEventArgs { Project = hierarchyWrapper, IsUnloadProcess = isUloadProcess, IsSolutionCloseProcess = fRemoved == 0 });

            return S_OK;
        }

        int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            var realHierarchyWrapper = pRealHierarchy.Wrap();
            if (!_projectConfigService.Value.IsSupportedProject(realHierarchyWrapper))
                return LogIgnoringUnsupportedProjectType();

            ProjectStateMap[realHierarchyWrapper.GetGuid()].IsLoaded = true;

            ProjectAfterLoad?.Invoke(this, realHierarchyWrapper);

            return S_OK;
        }

        int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            var realHierarchyWrapper = pRealHierarchy.Wrap();
            if (!_projectConfigService.Value.IsSupportedProject(realHierarchyWrapper))
                return LogIgnoringUnsupportedProjectType();

            ProjectStateMap[realHierarchyWrapper.GetGuid()].IsLoaded = false;

            ProjectBeforeUnload?.Invoke(this, realHierarchyWrapper);

            return S_OK;
        }


        int IVsSolutionEvents4.OnAfterRenameProject(IVsHierarchy pHierarchy)
        {
            var hierarchyWrapper = pHierarchy.Wrap();
            if (!_projectConfigService.Value.IsSupportedProject(hierarchyWrapper))
                return LogIgnoringUnsupportedProjectType();

            Guid projectGuid = hierarchyWrapper.GetGuid();
            var oldProjectDir = ProjectStateMap[projectGuid].ProjectDir;
            var oldProjectName = ProjectStateMap[projectGuid].ProjectName;
            ProjectStateMap[projectGuid].ProjectDir = hierarchyWrapper.GetProjectDir();
            ProjectStateMap[projectGuid].ProjectName = hierarchyWrapper.GetName();

            ProjectAfterRename?.Invoke(this, new ProjectAfterRenameEventArgs { OldProjectDir = oldProjectDir, OldProjectName = oldProjectName, Project = hierarchyWrapper });

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
