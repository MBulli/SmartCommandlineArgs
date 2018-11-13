//------------------------------------------------------------------------------
// <copyright file="ToolWindow.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace SmartCmdArgs
{
    using System;
    using System.ComponentModel.Design;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.PlatformUI;

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
    [Guid(ToolWindow.ToolWindowGuidString)]
    public class ToolWindow : ToolWindowPane, IVsWindowFrameNotify3, IVsWindowPaneCommit, IVsWindowPaneCommitFilter
    {
        public const string ToolWindowGuidString = "a21b35ed-5c13-4d55-a3d2-71054c4e9540";

        private View.ToolWindowControl view;

        private List<IVsWindowSearchOption> searchOptions;
        private WindowSearchBooleanOption matchCaseSearchOption;

        private new CmdArgsPackage Package
        {
            get { return (CmdArgsPackage)base.Package; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow"/> class.
        /// </summary>
        public ToolWindow() 
            : this(new ViewModel.ToolWindowViewModel())
        {
        }

        public ToolWindow(ViewModel.ToolWindowViewModel viewModel)
            : base(null)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            this.Caption = "Commandline Arguments";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.view = new View.ToolWindowControl(viewModel);
            this.Content = view;

            this.ToolBar = new CommandID(Commands.CmdArgsToolBarCmdSet, Commands.TWToolbar);

            matchCaseSearchOption = new WindowSearchBooleanOption("Match Case", "Enable to make search case sensitive.", false);
            searchOptions = new List<IVsWindowSearchOption> {matchCaseSearchOption};
        }

        public int OnShow(int fShow)
        {
            if (fShow == (int)__FRAMESHOW.FRAMESHOW_WinShown)
            {
                // TODO Obsolete ?!
                //view.ViewModel.UpdateView();
            }
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
            if (escapeDown && view.ViewModel.TreeViewModel.IsInEditMode)
            {
                pfCommitFailed = 1;
                view.ViewModel.TreeViewModel.CancelEditMode();
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
            return new SearchTask(dwCookie, pSearchQuery, pSearchCallback, this);
        }

        public override void ClearSearch()
        {
            Package.ToolWindowViewModel.TreeViewModel.SetStringFilter(null);
        }

        internal class SearchTask : VsSearchTask
        {
            private ToolWindow _toolWindow;

            public SearchTask(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback, ToolWindow toolwindow)
                : base(dwCookie, pSearchQuery, pSearchCallback)
            {
                _toolWindow = toolwindow;
            }

            protected override void OnStartSearch()
            {
                _toolWindow.Package.ToolWindowViewModel.TreeViewModel.SetStringFilter(SearchQuery.SearchString, _toolWindow.matchCaseSearchOption.Value);

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
