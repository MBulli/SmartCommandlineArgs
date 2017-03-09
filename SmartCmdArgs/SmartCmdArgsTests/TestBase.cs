using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VsSDK.IntegrationTestLibrary;
using Microsoft.VSSDK.Tools.VsIdeTesting;

namespace SmartCmdArgsTests
{
    public class TestBase
    {
        public const string HiveName = "15.0";

        protected DTE Dte => VsIdeTestHostContext.Dte;
        protected TestUtils Utils { get; } = new TestUtils();

        protected string RootDir => Directory.GetCurrentDirectory();

        protected object InvokeInUIThread(Action method)
            => UIThreadInvoker.Invoke(method);

        protected Interface GetService<Service, Interface>()
            where Interface : class
        {
            return VsIdeTestHostContext.ServiceProvider.GetService(typeof(Service)) as Interface;
        }
        
        protected Project CreateSolutionWithProject(string solutionName = "TestSolution", string projectName = "TestProject")
        {
            string solutionPath = Path.Combine(RootDir, solutionName);
            string projectPath = Path.Combine(solutionPath, projectName);

            if (Directory.Exists(solutionPath)) Directory.Delete(solutionPath, true);

            Solution2 solution = (Solution2)Dte.Solution;

            solution.Create(solutionPath, solutionName);

            var projectTemplate = solution.GetProjectTemplate("ConsoleApplication.zip", "CSharp");

            solution.AddFromTemplate(projectTemplate, projectPath, projectName);
            return solution.Projects.Item(1);
        }
    }
}
