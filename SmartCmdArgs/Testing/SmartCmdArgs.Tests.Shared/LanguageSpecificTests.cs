using System.Threading.Tasks;
using System.Linq;

using System;
using Xunit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace SmartCmdArgs.Tests.LanguageSpecificTests
{
    public abstract class LanguageSpecificTests : TestBase
    {
        private TestLanguage _language;

        public LanguageSpecificTests(TestLanguage language)
        {
            _language = language;
        }

        [VsFact]
        public async Task CollectArgsFromExistingProjectConfigsTest()
        {
            await OpenSolutionWithNameAsync(_language, "CollectArgsTest");

            var package = await LoadExtensionAsync();
            Assert.False(package?.ToolWindowViewModel?.SettingsViewModel?.VcsSupportEnabled, "VCS support must be disabled");

            var args = package?.ToolWindowViewModel?.TreeViewModel?.AllArguments?.ToList();

            Assert.NotNull(args);

            Assert.NotEmpty(args);

            Assert.All(args, x => Assert.StartsWith("args for ", x.Value));
            Assert.All(args, x => Assert.NotEqual(Guid.Empty, x.Id));
        }

        #region SetCmdArgsTests

        [VsFact]
        public async Task SetCommandLineArgsViaDebugTest()
        {
            await OpenSolutionWithNameAsync(_language, "ReadCmdArgsProject");
            var package = await LoadExtensionAsync();

            var args = new List<(string Arg, bool Enabled)>
            {
                ("Arg1", true),
                ("Arg42", false),
                ("Arg2", true),
            };

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var treeViewModel = package.ToolWindowViewModel.TreeViewModel;

            Assert.NotNull(treeViewModel);

            var project = treeViewModel.Projects.FirstOrDefault().Value;

            Assert.NotNull(project);

            project.InsertRange(0, args.Select(x => new ViewModel.CmdArgument(arg: x.Arg, isChecked: x.Enabled)));

            Dte.Solution.SolutionBuild.Build(true);

            var waiter = new TaskCompletionSource<object>();

            Dte.Events.DebuggerEvents.OnEnterDesignMode += reason => waiter.SetResult(null);
            Dte.ExecuteCommand("Debug.Start");

            // C++ and NodeJS projects do not fire OnEnterDesignMode
            // TODO: find a better fix for this
            if (_language == TestLanguage.CPP || _language == TestLanguage.NodeJS)
                await Task.Delay(3000);
            else
                await waiter.Task;

            Assert.True(File.Exists("ReadCmdArgsProject/CmdLineArgs.txt"));

            var cmdArgsFile = File.ReadAllText("ReadCmdArgsProject/CmdLineArgs.txt");
            var cmdArgsTest = string.Join(" ", args.Where(arg => arg.Enabled).Select(arg => arg.Arg));
            Assert.Equal(cmdArgsFile, cmdArgsTest);
        }

        #endregion SetCmdArgsTests
    }

    public class CSharp : LanguageSpecificTests
    {
        public CSharp() : base(TestLanguage.CSharpDotNetFW) { }
    }

    public class CSharp_Core : LanguageSpecificTests
    {
        public CSharp_Core() : base(TestLanguage.CSharpDotNetCore) { }
    }

    public class Cpp : LanguageSpecificTests
    {
        public Cpp() : base(TestLanguage.CPP) { }
    }

    public class VbDotNet : LanguageSpecificTests
    {
        public VbDotNet() : base(TestLanguage.VBDotNetFW) { }
    }

    public class NodeJs : LanguageSpecificTests
    {
        public NodeJs() : base(TestLanguage.NodeJS) { }
    }

    public class FSharp : LanguageSpecificTests
    {
        public FSharp() : base(TestLanguage.FSharpDotNetFW) { }
    }
}