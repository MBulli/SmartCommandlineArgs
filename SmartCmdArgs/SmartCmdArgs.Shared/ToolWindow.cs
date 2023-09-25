using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using Microsoft.VisualStudio.PlatformUI;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid(PackageGuids.guidToolWindowString)]
    public class ToolWindow : ToolWindowPane, IVsWindowFrameNotify3, IVsWindowPaneCommit, IVsWindowPaneCommitFilter
    {
        private readonly ToolWindowViewModel viewModel;
        private readonly TreeViewModel treeViewModel;

        private List<IVsWindowSearchOption> searchOptions;
        private WindowSearchBooleanOption matchCaseSearchOption;

        private new CmdArgsPackage Package
        {
            get { return (CmdArgsPackage)base.Package; }
        }

        internal ToolWindow(ToolWindowViewModel viewModel, TreeViewModel treeViewModel)
            : base(null)
        {
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            this.treeViewModel = treeViewModel;

            this.Caption = "Command Line Arguments";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new View.ToolWindowControl(viewModel);

            // Id from VSPackage.resx.
            BitmapResourceID = 300;

            // The index is actually zero-based, in contrast to the bitmaps in the vsct-file.
            BitmapIndex = 0;

            this.ToolBar = new CommandID(PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.TWToolbar);

            matchCaseSearchOption = new WindowSearchBooleanOption("Match Case", "Enable to make search case sensitive.", false);
            searchOptions = new List<IVsWindowSearchOption> {matchCaseSearchOption};
        }

        public int OnShow(int fShow)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnMove(int x, int y, int w, int h)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnSize(int x, int y, int w, int h)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnDockableChange(int fDockable, int x, int y, int w, int h)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        public int OnClose(ref uint pgrfSaveOptions)
        {
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        int IVsWindowPaneCommit.CommitPendingEdit(out int pfCommitFailed)
        {
            // This method is called when the user hits escape
            // If the datagrid is in EditMode we don't want to loose focus and just cancel the edit mode.
            // The tool window keeps the focus if pfCommitFailed==1

            bool escapeDown = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Escape);
            if (escapeDown && treeViewModel.IsInEditMode)
            {
                pfCommitFailed = 1;
                treeViewModel.CancelEditMode();
            }
            else
            {
                pfCommitFailed = 0;
            }

            return Microsoft.VisualStudio.VSConstants.S_OK;
        }

        int IVsWindowPaneCommitFilter.IsCommitCommand(ref Guid pguidCmdGroup, uint dwCmdID, out int pfCommitCommand)
        {
            pfCommitCommand = 1;
            return Microsoft.VisualStudio.VSConstants.S_OK;
        }


        public override bool SearchEnabled => true;

        public override IVsEnumWindowSearchOptions SearchOptionsEnum => new WindowSearchOptionEnumerator(searchOptions);

        public override IVsSearchTask CreateSearch(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
        {
            if (pSearchQuery == null || pSearchCallback == null)
                return null;
            return new SearchTask(dwCookie, pSearchQuery, pSearchCallback, this, treeViewModel);
        }

        public override void ClearSearch()
        {
            treeViewModel.SetStringFilter(null);
        }

        internal class SearchTask : VsSearchTask
        {
            private readonly ToolWindow toolWindow;
            private readonly TreeViewModel treeViewModel;

            public SearchTask(
                uint dwCookie,
                IVsSearchQuery pSearchQuery,
                IVsSearchCallback pSearchCallback,
                ToolWindow toolwindow,
                TreeViewModel treeViewModel)
                : base(dwCookie, pSearchQuery, pSearchCallback)
            {
                toolWindow = toolwindow;
                this.treeViewModel = treeViewModel;
            }

            protected override void OnStartSearch()
            {
                treeViewModel.SetStringFilter(SearchQuery.SearchString, toolWindow.matchCaseSearchOption.Value);

                // Call the implementation of this method in the base class.   
                // This sets the task status to complete and reports task completion.   
                base.OnStartSearch();
            }

            protected override void OnStopSearch()
            {
                this.SearchResults = 0;
            }
        }
    }
}
