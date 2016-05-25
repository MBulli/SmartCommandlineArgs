using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs
{
    class VisualStudioHelper : IVsUpdateSolutionEvents2, IVsSelectionEvents
    {
        /// <summary>
        /// Shortcut for Microsoft.VisualStudio.VSConstants.S_OK
        /// </summary>
        private const int S_OK = Microsoft.VisualStudio.VSConstants.S_OK;

        private CmdArgsPackage package;  
        private EnvDTE.DTE appObject;

        private EnvDTE.SolutionEvents solutionEvents;
        private EnvDTE.CommandEvents commandEvents;

        private IVsSolution5 solutionService;
        private IVsSolutionBuildManager2 solutionBuildService;
        private IVsMonitorSelection selectionMonitor;

        private bool initialized = false;
        private uint selectionEventsCookie = 0;
        private uint updateSolutionEventsCookie = 0;

        public EnvDTE.Solution Solution { get { return appObject.Solution; } }
        public bool IsSolutionOpen { get { return appObject?.Solution?.IsOpen ?? false; } }

        public event EventHandler SolutionOpend;
        public event EventHandler SolutionWillClose;
        public event EventHandler SolutionClosed;
        public event EventHandler StartupProjectChanged;
        public event EventHandler StartupProjectConfigurationChanged;

        public VisualStudioHelper(CmdArgsPackage package)
        {
            this.package = package;
            this.appObject = package.GetService<SDTE, EnvDTE.DTE>();

            // see: https://support.microsoft.com/en-us/kb/555430
            this.solutionEvents = this.appObject.Events.SolutionEvents;
            this.commandEvents = this.appObject.Events.CommandEvents;

            this.solutionEvents.Opened += SolutionEvents_Opened;
            this.solutionEvents.AfterClosing += SolutionEvents_AfterClosing;
            this.solutionEvents.BeforeClosing += SolutionEvents_BeforeClosing;
        }

        public void Initialize()
        {
            if (!initialized)
            {
                // Setup solution related stuff
                this.solutionService = package.GetService<SVsSolution, IVsSolution5>();
                this.solutionBuildService = package.GetService<SVsSolutionBuildManager, IVsSolutionBuildManager2>();
                this.selectionMonitor = package.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();

                // Set startp project

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

        public bool FindStartupProject(out EnvDTE.Project startupProject)
        {
            startupProject = null;

            string prjName = StartupProjectUniqueName();

            if (prjName != null)
            {
                startupProject = this.appObject?.Solution.Item(prjName);
                return true;
            }

            return false;           
        }

        public void UpdateShellCommandUI(bool immediateUpdate = true)
        {
            package.GetService<SVsUIShell, IVsUIShell>()?.UpdateCommandUI(immediateUpdate ? 1 : 0);
        }

        #region Solution Events
        private void SolutionEvents_Opened()
        {           
            SolutionOpend?.Invoke(this, EventArgs.Empty);
        }

        private void SolutionEvents_BeforeClosing()
        {
            SolutionWillClose?.Invoke(this, EventArgs.Empty);
        }

        private void SolutionEvents_AfterClosing()
        {
            SolutionClosed?.Invoke(this, EventArgs.Empty);
        }

        #endregion

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
            // This method is called for each Hierachy element (e.g. project and folders), thus we filter for the startup project
            // to only trigger the config changed event once.

            object objProj = null;
            pIVsHierarchy?.GetProperty(VSConstants.VSITEMID_ROOT,
                                       (int)__VSHPROPID.VSHPROPID_ExtObject,
                                       out objProj);

            EnvDTE.Project project = objProj as EnvDTE.Project;

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
    }
}
