﻿//------------------------------------------------------------------------------
// <copyright file="Commands.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;

using Task = System.Threading.Tasks.Task;

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
        public const int ToolbarAddGroupCommandId = 0x1105;
        public const int ToolbarShowAllProjectsCommandId = 0x1106;
        public const int ToolbarOpenSettingsCommandId = 0x1107;
        public const int ToolbarAddEnvVarId = 0x1108;
        public const int ToolbarCopyEnvVarsForPSCommandId = 0x1109;
        public const int ToolbarCopyEnvVarsForCMDCommandId = 0x110A;
        public const int ToolbarAddWorkDirId = 0x110B;
        public const int ToolbarAddLaunchAppId = 0x110C;

        public static readonly Guid KeyBindingsCmdSet = new Guid("886F463E-7F96-4BA4-BA88-F36D63044A00");

        public const int KeyBindingAddCmdId = 0x1200;

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly CmdArgsPackage package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(CmdArgsPackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));
            
            var cmdService = await package.GetServiceAsync<IMenuCommandService, OleMenuCommandService>();

            // AddCommand needs to be run on main thread!
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();

            Instance = new Commands(package, cmdService);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Commands"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private Commands(CmdArgsPackage package, OleMenuCommandService commandService)
        {
            this.package = package;

            if (commandService != null)
            {
                AddCommandToService(commandService, VSMenuCmdSet, ToolWindowCommandId, this.ShowToolWindow);

                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddCommandId, package.ToolWindowViewModel.AddEntryCommand, ViewModel.ArgumentType.CmdArg);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddEnvVarId, package.ToolWindowViewModel.AddEntryCommand, ViewModel.ArgumentType.EnvVar);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddWorkDirId, package.ToolWindowViewModel.AddEntryCommand, ViewModel.ArgumentType.WorkDir);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddLaunchAppId, package.ToolWindowViewModel.AddEntryCommand, ViewModel.ArgumentType.LaunchApp);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddGroupCommandId, package.ToolWindowViewModel.AddGroupCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarRemoveCommandId, package.ToolWindowViewModel.RemoveEntriesCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarMoveUpCommandId, package.ToolWindowViewModel.MoveEntriesUpCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarMoveDownCommandId, package.ToolWindowViewModel.MoveEntriesDownCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarCopyCommandlineCommandId, package.ToolWindowViewModel.CopyCommandlineCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarCopyEnvVarsForPSCommandId, package.ToolWindowViewModel.CopyEnvVarsForCommadlineCommand, "PS");
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarCopyEnvVarsForCMDCommandId, package.ToolWindowViewModel.CopyEnvVarsForCommadlineCommand, "CMD");
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarOpenSettingsCommandId, package.ToolWindowViewModel.ShowSettingsCommand);

                AddToggleCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarShowAllProjectsCommandId, 
                    package.ToolWindowViewModel.ShowAllProjectsCommand, () => package.ToolWindowViewModel.TreeViewModel.ShowAllProjects);
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

        private void AddCommandToService(OleMenuCommandService service, Guid cmdSet, int cmdId, EventHandler handler)
        {
            var commandId = new CommandID(cmdSet, cmdId);
            var menuCommand = new MenuCommand(handler, commandId);
            service.AddCommand(menuCommand);
        }

        private void AddCommandToService<T>(OleMenuCommandService service, Guid cmdSet, int cmdId, RelayCommand<T> relayCommand, T commandParameter = default)
        {
            var commandId = new CommandID(cmdSet, cmdId);
            var menuCommand = new OleMenuCommand((sender, args) =>
            {
                relayCommand.SafeExecute(commandParameter);
            }, commandId);

            menuCommand.BeforeQueryStatus += (sender, args) =>
            {
                menuCommand.Enabled = relayCommand.CanExecute(commandParameter);
            };

            service.AddCommand(menuCommand);
        }

        private void AddToggleCommandToService(OleMenuCommandService service, Guid cmdSet, int cmdId, RelayCommand relayCommand, Func<bool> isChecked)
        {
            var commandId = new CommandID(cmdSet, cmdId);
            var menuCommand = new OleMenuCommand((sender, args) =>
            {
                relayCommand.SafeExecute();
            }, commandId);

            menuCommand.BeforeQueryStatus += (sender, args) =>
            {
                menuCommand.Enabled = relayCommand.CanExecute(null);
                menuCommand.Checked = isChecked();
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
            ThreadHelper.ThrowIfNotOnUIThread();

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
