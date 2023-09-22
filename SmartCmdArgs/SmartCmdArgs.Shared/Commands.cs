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

        public static readonly Guid KeyBindingsCmdSet = new Guid("886F463E-7F96-4BA4-BA88-F36D63044A00");
        public const int KeyBindingAddCmdId = 0x1200;

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
                AddCommandToService(commandService, VSMenuCmdSet, ToolWindowCommandId, this.ShowToolWindow);

                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddCommandId, toolWindowViewModel.AddEntryCommand, ViewModel.ArgumentType.CmdArg);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddEnvVarId, toolWindowViewModel.AddEntryCommand, ViewModel.ArgumentType.EnvVar);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddWorkDirId, toolWindowViewModel.AddEntryCommand, ViewModel.ArgumentType.WorkDir);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarAddGroupCommandId, toolWindowViewModel.AddGroupCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarRemoveCommandId, toolWindowViewModel.RemoveEntriesCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarMoveUpCommandId, toolWindowViewModel.MoveEntriesUpCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarMoveDownCommandId, toolWindowViewModel.MoveEntriesDownCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarCopyCommandlineCommandId, toolWindowViewModel.CopyCommandlineCommand);
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarCopyEnvVarsForPSCommandId, toolWindowViewModel.CopyEnvVarsForCommadlineCommand, "PS");
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarCopyEnvVarsForCMDCommandId, toolWindowViewModel.CopyEnvVarsForCommadlineCommand, "CMD");
                AddCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarOpenSettingsCommandId, toolWindowViewModel.ShowSettingsCommand);

                AddToggleCommandToService(commandService, CmdArgsToolBarCmdSet, ToolbarShowAllProjectsCommandId,
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
            windowFrame.SetProperty((int)__VSFPROPID.VSFPROPID_CmdUIGuid, ToolWindow.ToolWindowGuidString);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
