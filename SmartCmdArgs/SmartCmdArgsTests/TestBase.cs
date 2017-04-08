using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VsSDK.IntegrationTestLibrary;
using Microsoft.VSSDK.Tools.VsIdeTesting;
using SmartCmdArgs;
using SmartCmdArgsTests.Utils;

namespace SmartCmdArgsTests
{
    public class TestBase
    {
        public const string HiveName = "15.0";
        public const string HiveStartArgs = @"/RootSuffix Exp /ResetSettings";

        protected DTE Dte => VsIdeTestHostContext.Dte;
        protected TestUtils Utils { get; } = new TestUtils();

        protected string RootDir => Directory.GetCurrentDirectory();

        protected object InvokeInUIThread(Action method)
            => UIThreadInvoker.Invoke(method);

        protected CmdArgsPackage ExtensionPackage;

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

            var projectTemplate = solution.GetProjectTemplate("EmptyProject.zip", "CSharp");

            solution.AddFromTemplate(projectTemplate, projectPath, projectName);
            return solution.Projects.Item(1);
        }

        protected bool OpenSolutionWithPath(string solutionFile)
        {
            Assert.IsTrue(File.Exists(solutionFile), $"No Solution file at path: {solutionFile}");

            using (var waiting = new WaitUntil())
            {
                Dte.Events.SolutionEvents.Opened += () => waiting.Finish();
                
                Dte.ExecuteCommand("File.OpenProject", $"\"{solutionFile}\"");
            }
            return true;
        }

        protected const string BaseTestSolutionPath = @"..\..\..\TestSolutions";

        protected bool OpenSolutionWithName(TestLanguage language, string solutionName, bool overrideExistingTemp = true)
        {
            var langFolder = TranslateLanguageFolderName(language);

            var path = Path.GetFullPath(Path.Combine(BaseTestSolutionPath, langFolder, solutionName));
            Assert.IsTrue(Directory.Exists(path), $"No Solution folder at path: {path}");
            if (overrideExistingTemp)
            {
                if (Directory.Exists(solutionName))
                    Directory.Delete(solutionName, true);
                Helper.DirectoryCopy(path, solutionName);
            }
            return OpenSolutionWithPath(Path.GetFullPath(Path.Combine(solutionName, "Solution.sln")));
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

        protected void LoadExtension()
        {
            ExtensionPackage = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            SetVcsSupport(true);
        }

        protected void SetVcsSupport(bool enabled)
        {
            var properties = (CmdArgsOptionPage)ExtensionPackage.GetDialogPage(typeof(CmdArgsOptionPage));
            InvokeInUIThread(() => properties.VcsSupport = enabled);
        }


        /// <summary>
        /// Helper method which is used to invoke a concrete test method on TClass
        /// </summary>
        public static void RunTest<TClass>(Action<TClass> test)
            where TClass : new()
        {
            test(new TClass());
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
