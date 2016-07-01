using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VSSDK.Tools.VsIdeTesting;
using SmartCmdArgs;

namespace SmartCmdArgsTests
{
    [TestClass]
    public class VcsSupportTests : TestBase
    {
        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, "14.0Exp")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void SaveCommandsToJsonTest()
        {
            string solutionName = "SaveCommandsToJsonTestSolution";
            string projectName = "SaveCommandsToJsonTestProject";
            CreateSolutionWithProject(solutionName, projectName);

            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));

            InvokeInUIThread(() =>
            {
                var curList = package?.ToolWindowViewModel?.CurrentArgumentList;
                Assert.IsNotNull(curList);

                var initalCommands = new[] { "arg1", "Arg2", "arg 3" };

                foreach (var initalCommand in initalCommands)
                {
                    package.ToolWindowViewModel.CurrentArgumentList.AddNewItem(initalCommand);
                }

                Utils.ForceSaveSolution();

                string jsonFile = Path.Combine(Path.Combine(Path.Combine(
                        RootDir,
                        solutionName),
                        projectName),
                        projectName + ".args.json");

                CheckJsonFile(jsonFile, initalCommands);
            });
        }


        private void CheckJsonFile(string jsonFile, string[] commands)
        {
            var jsonRegex = new Regex("\\{\\s*\"DataCollection\":\\s*\\[(?:\\s*\\{\\s*\"Id\":\\s*\"(?<id>[^\"]*)\",\\s*\"Command\":\\s*\"(?<command>.*?)\"\\s*\\}\\s*,?)*\\s*\\]\\s*\\}", RegexOptions.Compiled);

            Assert.IsTrue(File.Exists(jsonFile));

            string jsonFileContent = File.ReadAllText(jsonFile);

            var match = jsonRegex.Match(jsonFileContent);

            Assert.IsNotNull(match);
            Assert.IsTrue(match.Success);

            var mathedIds = match.Groups["id"]?.Captures;
            var matchedCommands = match.Groups["command"]?.Captures;

            Assert.IsNotNull(mathedIds);
            Assert.IsNotNull(matchedCommands);
            Assert.AreEqual(commands.Length, mathedIds.Count);
            Assert.AreEqual(commands.Length, matchedCommands.Count);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(mathedIds[i].Value));
                Assert.AreNotEqual(Guid.Empty.ToString(), mathedIds[i].Value);

                Assert.IsFalse(string.IsNullOrEmpty(matchedCommands[i].Value));
                Assert.AreEqual(commands[i], matchedCommands[i].Value);
            }
        }
    }
}
