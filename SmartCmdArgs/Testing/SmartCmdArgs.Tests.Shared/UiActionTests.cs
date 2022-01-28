using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace SmartCmdArgs.Tests
{
    public class UiActionTests : TestBase
    {
        [VsFact]
        public async Task AddNewArgLineViaCommandTest()
        {
            await OpenSolutionWithNameAsync(TestLanguage.CSharpDotNetCore, "DefaultProject");

            var package = await LoadExtensionAsync();

            var addCommand = package?.ToolWindowViewModel?.AddEntryCommand;
            Assert.NotNull(addCommand);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Assert.True(addCommand.CanExecute(null));

            addCommand.Execute(null);

            var args = package?.ToolWindowViewModel?.TreeViewModel?.AllArguments?.ToList();

            Assert.NotNull(args);

            Assert.True(args.Count == 1);

            var argItem = args[0];
            Assert.NotEqual(Guid.Empty, argItem.Id);
            Assert.Equal("", argItem.Value);
        }
    }
}
