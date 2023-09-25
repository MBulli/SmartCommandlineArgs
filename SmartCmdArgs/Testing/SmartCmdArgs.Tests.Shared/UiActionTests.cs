using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.ViewModel;
using System;
using System.Linq;
using Xunit;

using Task = System.Threading.Tasks.Task;

namespace SmartCmdArgs.Tests
{
    public class UiActionTests : TestBase
    {
        [VsFact(Skip = IntegrationTestSkip)]
        public async Task AddNewArgLineViaCommandTest()
        {
            await OpenSolutionWithNameAsync(TestLanguage.CSharpDotNetCore, "DefaultProject");

            var package = await LoadExtensionAsync();

            var toolWindowViewModel = package.ServiceProvider.GetService<ToolWindowViewModel>();

            var addCommand = toolWindowViewModel?.AddEntryCommand;
            Assert.NotNull(addCommand);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Assert.True(addCommand.CanExecute(null));

            addCommand.Execute(null);

            var treeViewModel = package.ServiceProvider.GetService<TreeViewModel>();
            var args = treeViewModel?.AllParameters?.ToList();

            Assert.NotNull(args);

            Assert.True(args.Count == 1);

            var argItem = args[0];
            Assert.NotEqual(Guid.Empty, argItem.Id);
            Assert.Equal("", argItem.Value);
        }
    }
}
