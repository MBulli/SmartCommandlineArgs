using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VsSDK.IntegrationTestLibrary;
using Microsoft.VSSDK.Tools.VsIdeTesting;
using SmartCmdArgs;

namespace SmartCmdArgsTests
{
    [TestClass]
    public class UnitTest1
    {
        private DTE Dte => VsIdeTestHostContext.Dte;
        private TestUtils Utils { get; } = new TestUtils();


        private object InvokeInUIThread(Action method)
            => UIThreadInvoker.Invoke(method);

        private Interface GetService<Service, Interface>()
            where Interface : class
        {
            return VsIdeTestHostContext.ServiceProvider.GetService(typeof(Service)) as Interface;
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, "14.0Exp")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void CollectArgsFromExistingProjectConfigsTest()
        {
            Project project = CreateSolutionWithProject("CollectTestSolution", "CollectTestProject");

            List<string> startArgumentsForEachConfig = new List<string>();
            foreach (Configuration config in project.ConfigurationManager)
            {
                string startArguments = $"args for {config.ConfigurationName}";
                Debug.WriteLine($"Adding args '{startArguments}' to configuration '{config.ConfigurationName}'");
                startArgumentsForEachConfig.Add(startArguments);
                config.Properties.Item("StartArguments").Value = startArguments;
            }

            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;
            Assert.IsNotNull(argItems);

            Assert.AreEqual(startArgumentsForEachConfig.Count, argItems.Count);

            foreach (var startArguments in startArgumentsForEachConfig)
            {
                var argItem = argItems.FirstOrDefault(item => item.Command == startArguments);
                
                Assert.IsNotNull(argItem);
                Assert.AreNotEqual(Guid.Empty, argItem.Id);
            }
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, "14.0Exp")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void CollectArgsDistinctFromExistingProjectConfigsTest()
        {
            const string startArguments = "same args in every config";

            Project project = CreateSolutionWithProject("CollectDistinctTestSolution", "CollectDistinctTestProject");
            
            foreach (Configuration config in project.ConfigurationManager)
            {
                Debug.WriteLine($"Adding args '{startArguments}' to configuration '{config.ConfigurationName}'");
                config.Properties.Item("StartArguments").Value = startArguments;
            }

            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;
            Assert.IsNotNull(argItems);

            Assert.AreEqual(1, argItems.Count);

            var argItem = argItems[0];
            Assert.AreNotEqual(Guid.Empty, argItem.Id);
            Assert.AreEqual(startArguments, argItem.Command);
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, "14.0Exp")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void AddNewArgLineViaCommandTest()
        {
            CreateSolutionWithProject("CollectDistinctTestSolution", "CollectDistinctTestProject");

            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));

            ICommand addCommand = package?.ToolWindowViewModel?.AddEntryCommand;
            Assert.IsNotNull(addCommand);

            InvokeInUIThread(() =>
            {
                Assert.IsTrue(package.ToolWindowViewModel.AddEntryCommand.CanExecute(null));
                addCommand.Execute(null);

                var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;
                Assert.IsNotNull(argItems);

                Assert.AreEqual(1, argItems.Count);

                var argItem = argItems[0];
                Assert.AreNotEqual(Guid.Empty, argItem.Id);
                Assert.AreEqual("", argItem.Command);
            });
        }

        private Project CreateSolutionWithProject(string solutionName = "TestSolution", string projectName = "TestProject")
        {
            Debug.WriteLine("CreateSolutionWithProject");

            string rootPath = Directory.GetCurrentDirectory();

            string solutionPath = Path.Combine(rootPath, solutionName);
            string projectPath = Path.Combine(solutionPath, projectName);

            if(Directory.Exists(solutionPath)) Directory.Delete(solutionPath, true);

            Solution2 solution = (Solution2)Dte.Solution;

            solution.Create(solutionPath, solutionName);

            var projectTemplate = solution.GetProjectTemplate("ConsoleApplication.zip", "CSharp");

            solution.AddFromTemplate(projectTemplate, projectPath, projectName);
            return solution.Projects.Item(1);
        }
    }
}
