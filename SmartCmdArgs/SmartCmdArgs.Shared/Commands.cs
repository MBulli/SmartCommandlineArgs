using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Services;
using SmartCmdArgs.ViewModel;
using Task = System.Threading.Tasks.Task;

namespace SmartCmdArgs
{
    internal sealed class Commands : IAsyncInitializable
    {
        private readonly ToolWindowViewModel toolWindowViewModel;
        private readonly TreeViewModel treeViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="Commands"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        public Commands(
            ToolWindowViewModel toolWindowViewModel,
            TreeViewModel treeViewModel)
        {
            this.toolWindowViewModel = toolWindowViewModel;
            this.treeViewModel = treeViewModel;
        }

        public async Task InitializeAsync()
        {
            var commandService = await CmdArgsPackage.Instance.GetServiceAsync<IMenuCommandService, OleMenuCommandService>();

            if (commandService != null)
            {
                AddCommandToService(commandService, PackageGuids.guidVSMenuCmdSet, PackageIds.ToolWindowCommandId, this.ShowToolWindow);

                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarAddCommandId, toolWindowViewModel.AddEntryCommand, ViewModel.CmdParamType.CmdArg);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarAddEnvVarId, toolWindowViewModel.AddEntryCommand, ViewModel.CmdParamType.EnvVar);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarAddWorkDirId, toolWindowViewModel.AddEntryCommand, ViewModel.CmdParamType.WorkDir);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarAddGroupCommandId, toolWindowViewModel.AddGroupCommand);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarRemoveCommandId, toolWindowViewModel.RemoveEntriesCommand);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarMoveUpCommandId, toolWindowViewModel.MoveEntriesUpCommand);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarMoveDownCommandId, toolWindowViewModel.MoveEntriesDownCommand);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarCopyCommandlineCommandId, toolWindowViewModel.CopyCommandlineCommand);
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarCopyEnvVarsForPSCommandId, toolWindowViewModel.CopyEnvVarsForCommadlineCommand, "PS");
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarCopyEnvVarsForCMDCommandId, toolWindowViewModel.CopyEnvVarsForCommadlineCommand, "CMD");
                AddCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarOpenSettingsCommandId, toolWindowViewModel.ShowSettingsCommand);

                AddToggleCommandToService(commandService, PackageGuids.guidCmdArgsToolBarCmdSet, PackageIds.ToolbarShowAllProjectsCommandId,
                    toolWindowViewModel.ShowAllProjectsCommand, () => treeViewModel.ShowAllProjects);
            }

            // AddCommand needs to be run on main thread!
            await CmdArgsPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
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
            ToolWindowPane window = CmdArgsPackage.Instance.FindToolWindow(typeof(ToolWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            windowFrame.SetProperty((int)__VSFPROPID.VSFPROPID_CmdUIGuid, PackageGuids.guidToolWindowString);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
