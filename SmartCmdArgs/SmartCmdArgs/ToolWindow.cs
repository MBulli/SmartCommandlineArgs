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
    [Guid("a21b35ed-5c13-4d55-a3d2-71054c4e9540")]
    public class ToolWindow : ToolWindowPane, IVsWindowFrameNotify3
    {
        private View.ToolWindowControl view;

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
    }
}
