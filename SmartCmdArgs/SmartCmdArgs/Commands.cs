//------------------------------------------------------------------------------
// <copyright file="Commands.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;

namespace SmartCmdArgs
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class Commands
    {

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid VSMenuCmdSet = new Guid("C5334667-5DDA-4F4A-BC24-6E0084DC5068");

        public const int ToolWindowCommandId = 0x0100;


        public static readonly Guid CmdArgsToolBarCmdSet = new Guid("53D59879-7413-491E-988C-938117B773E3");

        public const int TWToolbar = 0x1000;
        public const int TWToolbarGroup = 0x1050;
        public const int ToolbarAddCommandId = 0x1100;
        public const int ToolbarRemoveCommandId = 0x1101;
        public const int ToolbarMoveUpCommandId = 0x1102;
        public const int ToolbarMoveDownCommandId = 0x1103;
        public const int ToolbarCopyCommandlineCommandId = 0x1104;
        public const int ToolbarAddGroupId = 0x1105;

        public static readonly Guid KeyBindingsCmdSet = new Guid("886F463E-7F96-4BA4-BA88-F36D63044A00");

        public const int KeyBindingAddCmdId = 0x1200;

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly CmdArgsPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="Commands"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private Commands(CmdArgsPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                AddCommandToService(commandService, VSMenuCmdSet, ToolWindowCommandId, this.ShowToolWindow);

                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddCommandId, package.ToolWindowViewModel.AddEntryCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddGroupId, package.ToolWindowViewModel.AddGroupCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarRemoveCommandId, package.ToolWindowViewModel.RemoveEntriesCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarMoveUpCommandId, package.ToolWindowViewModel.MoveEntriesUpCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarMoveDownCommandId, package.ToolWindowViewModel.MoveEntriesDownCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarCopyCommandlineCommandId, package.ToolWindowViewModel.CopyCommandlineCommand);
                AddCommandToService(commandService, KeyBindingsCmdSet, KeyBindingAddCmdId, package.ToolWindowViewModel.AddEntryCommand);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static Commands Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(CmdArgsPackage package)
        {
            Instance = new Commands(package);
        }

        private void AddCommandToService(OleMenuCommandService service, Guid cmdSet, int cmdId, EventHandler handler)
        {
            var commandId = new CommandID(cmdSet, cmdId);
            var menuCommand = new MenuCommand(handler, commandId);
            service.AddCommand(menuCommand);
        }

        private void AddCommandToService(OleMenuCommandService service, Guid cmdSet, int cmdId, RelayCommand relayCommand)
        {
            var commandId = new CommandID(cmdSet, cmdId);
            var menuCommand = new OleMenuCommand((sender, args) =>
            {
                relayCommand.Execute(null);
            }, commandId);

            menuCommand.BeforeQueryStatus += (sender, args) =>
            {
                menuCommand.Enabled = relayCommand.CanExecute(null);
            };

            service.AddCommand(menuCommand);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void ShowToolWindow(object sender, EventArgs e)
        {
            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = this.package.FindToolWindow(typeof(ToolWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            windowFrame.SetProperty((int)__VSFPROPID.VSFPROPID_CmdUIGuid, ToolWindow.ToolWindowGuidString);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
