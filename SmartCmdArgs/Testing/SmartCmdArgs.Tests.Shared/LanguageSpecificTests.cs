using System.Threading.Tasks;
using System.Linq;
using System;
using Xunit;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.IO;
using SmartCmdArgs.ViewModel;
using Microsoft.Extensions.DependencyInjection;

using Task = System.Threading.Tasks.Task;

namespace SmartCmdArgs.Tests.LanguageSpecificTests
{
    public abstract class LanguageSpecificTests : TestBase
    {
        private TestLanguage _language;

        public LanguageSpecificTests(TestLanguage language)
        {
            _language = language;
        }

        [VsFact(Skip = IntegrationTestSkip)]
        public async Task CollectArgsFromExistingProjectConfigsTest()
        {
            await OpenSolutionWithNameAsync(_language, "CollectArgsTest");

            var package = await LoadExtensionAsync();
            var settingsViewModel = package.ServiceProvider.GetService<SettingsViewModel>();

            Assert.False(settingsViewModel?.VcsSupportEnabled, "VCS support must be disabled");

            var treeViewModel = package.ServiceProvider.GetService<TreeViewModel>();
            var args = treeViewModel?.AllParameters?.ToList();

            Assert.NotNull(args);

            Assert.NotEmpty(args);

            Assert.All(args, x => Assert.StartsWith("args for ", x.Value));
            Assert.All(args, x => Assert.NotEqual(Guid.Empty, x.Id));
        }

        #region SetCmdArgsTests

        [VsFact(Skip = IntegrationTestSkip)]
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

            var treeViewModel = package.ServiceProvider.GetService<TreeViewModel>();
            var project = treeViewModel?.Projects.FirstOrDefault().Value;

            Assert.NotNull(project);

            project.InsertRange(0, args.Select(x => new ViewModel.CmdParameter(paramType: ViewModel.CmdParamType.CmdArg, value: x.Arg, isChecked: x.Enabled)));

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