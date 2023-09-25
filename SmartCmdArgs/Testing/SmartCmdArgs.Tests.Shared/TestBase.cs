using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

using ServiceProvider = Microsoft.VisualStudio.Shell.ServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace SmartCmdArgs.Tests
{
    [VsTestSettings(ReuseInstance = false, Version = Utils.Config.Version)]
    public class TestBase
    {
        protected const string IntegrationTestSkip = "Integration Testing does not work ATM";

        protected DTE Dte => GetService<DTE, DTE>();

        protected string RootDir => Directory.GetCurrentDirectory();

        protected CmdArgsPackage ExtensionPackage;

        protected Interface GetService<Service, Interface>()
            where Interface : class
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var service = ServiceProvider.GlobalProvider.GetService(typeof(Service)) as Interface;
            Assumes.Present(service);

            return service;
        }

        protected Project CreateSolutionWithProject(string solutionName = "TestSolution", string projectName = "TestProject")
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionPath = Path.Combine(RootDir, solutionName);
            string projectPath = Path.Combine(solutionPath, projectName);

            if (Directory.Exists(solutionPath)) Directory.Delete(solutionPath, true);

            Solution2 solution = (Solution2)Dte.Solution;

            solution.Create(solutionPath, solutionName);

            var projectTemplate = solution.GetProjectTemplate("EmptyProject.zip", "CSharp");

            solution.AddFromTemplate(projectTemplate, projectPath, projectName);
            return solution.Projects.Item(1);
        }

        protected async Task OpenSolutionWithPathAsync(string solutionFile)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Assert.True(File.Exists(solutionFile), $"No Solution file at path: {solutionFile}");

            var taskCompletionSource = new TaskCompletionSource<object>();

            Dte.Events.SolutionEvents.Opened += () => taskCompletionSource.SetResult(null);

            Dte.ExecuteCommand("File.OpenProject", $"\"{solutionFile}\"");

            await taskCompletionSource.Task;
        }

        protected const string BaseTestSolutionPath = @"..\..\..\..\TestSolutions";

        protected async Task OpenSolutionWithNameAsync(TestLanguage language, string solutionName, bool overrideExistingTemp = true)
        {
            var langFolder = TranslateLanguageFolderName(language);

            var path = Path.GetFullPath(Path.Combine(BaseTestSolutionPath, langFolder, solutionName));
            Assert.True(Directory.Exists(path), $"No Solution folder at path: {path}");
            if (overrideExistingTemp)
            {
                if (Directory.Exists(solutionName))
                    Directory.Delete(solutionName, true);

                Utils.Helper.DirectoryCopy(path, solutionName);
            }

            await OpenSolutionWithPathAsync(Path.GetFullPath(Path.Combine(solutionName, "Solution.sln")));
        }

        private string TranslateLanguageFolderName(TestLanguage lang)
        {
            switch (lang)
            {
                case TestLanguage.CSharpDotNetFW: return "C#_.NetFW";
                case TestLanguage.CSharpDotNetCore: return "C#_.NetCore";
                case TestLanguage.CPP: return "C++";
                case TestLanguage.VBDotNetFW: return "VB_.NetFW";
                case TestLanguage.NodeJS: return "Node.js";
                case TestLanguage.FSharpDotNetFW: return "F#_.NetFW";
                default: throw new ArgumentException("Unknown TestLanguage");
            }
        }

        protected async Task<CmdArgsPackage> LoadExtensionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var shell = GetService<SVsShell, IVsShell>();

            var packageGuid = Guid.Parse(PackageGuids.guidCmdArgsToolWindowPackageString);

            if (VSConstants.S_OK == shell.IsPackageLoaded(ref packageGuid, out IVsPackage loadedPackage))
            {
                ExtensionPackage = loadedPackage as CmdArgsPackage;
                Assert.NotNull(loadedPackage);
            }
            else
            {
                ExtensionPackage = await (shell as IVsShell7).LoadPackageAsync(ref packageGuid) as CmdArgsPackage;
            }

            Assert.NotNull(ExtensionPackage);

            return ExtensionPackage;
        }

        protected void SetVcsSupport(bool enabled)
        {
            var settingsViewModel = ExtensionPackage.ServiceProvider.GetService<SettingsViewModel>();
            settingsViewModel.VcsSupportEnabled = enabled;
        }


        /// <summary>
        /// Helper method which is used to invoke a concrete test method on TClass
        /// </summary>
        public static async Task RunTestAsync<TClass>(Func<TClass, Task> test)
            where TClass : new()
        {
            await test(new TClass());
        }
    }

    public enum TestLanguage
    {
        /// <summary>
        /// C# project targeting .Net Framework
        /// </summary>
        CSharpDotNetFW,
        /// <summary>
        /// C# project targeting .Net Core
        /// </summary>
        CSharpDotNetCore,
        /// <summary>
        /// C++ project
        /// </summary>
        CPP,
        /// <summary>
        /// Visual Basic .Net project targeting .Net Framework
        /// </summary>
        VBDotNetFW,
        /// <summary>
        /// NodeJs project
        /// </summary>
        NodeJS,
        /// <summary>
        /// F# project targeting .Net Framework
        /// </summary>
        FSharpDotNetFW
    }
}
