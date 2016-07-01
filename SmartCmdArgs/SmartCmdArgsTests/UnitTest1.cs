using System;
using System.IO;
using System.Text.RegularExpressions;
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
        [TestProperty("VsHiveName", "14.0Exp")]
        public void CollectArgsFromExistingProjectConfigsTest()
        {
            string rootPath = Directory.GetCurrentDirectory();
            string solutionName = "TestSolution";
            string projectName = "TestProject";

            string solutionPath = Path.Combine(rootPath, solutionName);
            string projectPath = Path.Combine(solutionPath, projectName);

            Solution solution = Dte.Solution;

            solution.Create(solutionPath, solutionName);

            Solution2 solution2 = (Solution2)solution;
            var projectTemplate = solution2.GetProjectTemplate("ConsoleApplication.zip", "CSharp");

            solution.AddFromTemplate(projectTemplate, projectPath, projectName);
            Project project = solution.Projects.Item(1);

            EnvDTE.Properties properties = project.ConfigurationManager?.ActiveConfiguration?.Properties;
            properties.Item("StartArguments").Value = "bla lol 3.Arg";

            IVsPackage package = Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            Assert.IsNotNull(package);

            solution.SaveAs(Path.Combine(solutionPath, solutionName + ".sln"));

            string jsonContent = File.ReadAllText(Path.Combine(projectPath, projectName + ".args.json"));

            Regex jsonRegex = new Regex("\\{\\s*\"DataCollection\":\\s*\\[\\s*\\{\\s*\"Id\":\\s*\"[^\"]*\",\\s*\"Command\":\\s*\"bla lol 3\\.Arg\"\\s*\\}\\s*\\]\\s*\\}");

            Assert.IsTrue(jsonRegex.IsMatch(jsonContent));
        }
    }
}
