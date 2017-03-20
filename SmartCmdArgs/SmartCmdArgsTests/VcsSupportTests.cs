using System;
using System.IO;
using System.Text.RegularExpressions;
using EnvDTE;
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
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions,
            VsIdeTestHostContants.HostRestartOptions.Before)]
        public void SaveCommandsToJsonTest()
        {
            var project = CreateSolutionWithProject();

            var package = (CmdArgsPackage) Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));

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

                string jsonFile = JsonFileFromProject(project);

                CheckJsonFile(jsonFile, initalCommands);
            });
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void LoadCommandsFromExistingJsonTest()
        {
            var project = CreateSolutionWithProject();
            var jsonFile = JsonFileFromProject(project);
            
            File.WriteAllText(jsonFile, @"
{
  ""DataCollection"": [
    {
      ""Id"": ""ac3f6619-4027-4417-935c-824c3a45e604"",
      ""Command"": ""Imported Args""
    },
    {
      ""Id"": ""2a47c412-f43d-45f7-b248-6aa8cf233c30"",
      ""Command"": ""second imported arg""
    },
    {
      ""Command"": ""imported arg without id""
    }
  ]
}");

            var package = (CmdArgsPackage) Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));

            var currentList = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;
            Assert.IsNotNull(currentList);
            Assert.AreEqual(3, currentList.Count);

            var arg1 = currentList[0];
            Assert.AreEqual("Imported Args", arg1.Command);
            Assert.AreEqual(new Guid("ac3f6619-4027-4417-935c-824c3a45e604"), arg1.Id);

            var arg2 = currentList[1];
            Assert.AreEqual("second imported arg", arg2.Command);
            Assert.AreEqual(new Guid("2a47c412-f43d-45f7-b248-6aa8cf233c30"), arg2.Id);

            var arg3 = currentList[2];
            Assert.AreEqual("imported arg without id", arg3.Command);
            Assert.AreNotEqual(Guid.Empty, arg3.Id);
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void LoadCommandsFromCreatedJsonTest()
        {
            var project = CreateSolutionWithProject();
            var jsonFile = JsonFileFromProject(project);
            
            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));

            File.WriteAllText(jsonFile, @"
{
  ""DataCollection"": [
    {
      ""Id"": ""ac3f6619-4027-4417-935c-824c3a45e604"",
      ""Command"": ""Imported Args""
    },
    {
      ""Id"": ""2a47c412-f43d-45f7-b248-6aa8cf233c30"",
      ""Command"": ""second imported arg""
    },
    {
      ""Command"": ""imported arg without id""
    }
  ]
}");

            System.Threading.Thread.Sleep(1000);

            var currentList = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;
            Assert.IsNotNull(currentList);
            Assert.AreEqual(3, currentList.Count);

            var arg1 = currentList[0];
            Assert.AreEqual("Imported Args", arg1.Command);
            Assert.AreEqual(new Guid("ac3f6619-4027-4417-935c-824c3a45e604"), arg1.Id);

            var arg2 = currentList[1];
            Assert.AreEqual("second imported arg", arg2.Command);
            Assert.AreEqual(new Guid("2a47c412-f43d-45f7-b248-6aa8cf233c30"), arg2.Id);

            var arg3 = currentList[2];
            Assert.AreEqual("imported arg without id", arg3.Command);
            Assert.AreNotEqual(Guid.Empty, arg3.Id);
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void LoadChangesFromJsonTest()
        {
            var project = CreateSolutionWithProject();
            var jsonFile = JsonFileFromProject(project);
            
            File.WriteAllText(jsonFile, @"
{
  ""DataCollection"": [
    {
      ""Id"": ""ac3f6619-4027-4417-935c-824c3a45e604"",
      ""Command"": ""Imported Args""
    },
    {
      ""Id"": ""2a47c412-f43d-45f7-b248-6aa8cf233c30"",
      ""Command"": ""second imported arg""
    },
    {
      ""Command"": ""imported arg without id""
    }
  ]
}");

            var package = (CmdArgsPackage) Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));

            using (var writer = new StreamWriter(File.Open(jsonFile, FileMode.Truncate)))
            {
                writer.Write(@"
{
  ""DataCollection"": [
    {
      ""Id"": ""ac3f6619-4027-4417-935c-824c3a45e604"",
      ""Command"": ""Imported Args""
    },
    {
      ""Id"": ""2a47c412-f43d-45f7-b248-6aa8cf233c30"",
      ""Command"": ""second imported arg changed""
    },
    {
      ""Command"": ""imported arg without id also changed""
    }
  ]
}");
            }
            System.Threading.Thread.Sleep(1000);

            var currentList = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;
            Assert.IsNotNull(currentList);
            Assert.AreEqual(3, currentList.Count);

            var arg1 = currentList[0];
            Assert.AreEqual("Imported Args", arg1.Command);
            Assert.AreEqual(new Guid("ac3f6619-4027-4417-935c-824c3a45e604"), arg1.Id);

            var arg2 = currentList[1];
            Assert.AreEqual("second imported arg changed", arg2.Command);
            Assert.AreEqual(new Guid("2a47c412-f43d-45f7-b248-6aa8cf233c30"), arg2.Id);

            var arg3 = currentList[2];
            Assert.AreEqual("imported arg without id also changed", arg3.Command);
            Assert.AreNotEqual(Guid.Empty, arg3.Id);

        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void AvoidEmptyJsonFileTest()
        {
            var project = CreateSolutionWithProject();
            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            
            Utils.ForceSaveSolution();

            Assert.IsFalse(File.Exists(JsonFileFromProject(project)));
        }
        
        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void DisabledVcsSupportTest()
        {
            var project = CreateSolutionWithProject();
            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            var properties = (CmdArgsOptionPage)package.GetDialogPage(typeof(CmdArgsOptionPage));

            properties.VcsSupport = false;

            InvokeInUIThread(() =>
            {
                var currentList = package?.ToolWindowViewModel?.CurrentArgumentList;
                Assert.IsNotNull(currentList);

                currentList.AddNewItem("TestItem1");
                currentList.AddNewItem("TestItem2");

                Utils.ForceSaveSolution();

                Assert.IsFalse(File.Exists(JsonFileFromProject(project)));
            });
        }


        private string JsonFileFromProject(Project project)
        {
            string filename = $"{Path.GetFileNameWithoutExtension(project.FullName)}.args.json";
            return Path.Combine(Path.GetDirectoryName(project.FullName), filename);
        }

        private void CheckJsonFile(string jsonFile, string[] commands)
        {
            var jsonRegex = new Regex(@"\{\s*""DataCollection"":\s*\[(?:\s*\{\s*""Id"":\s*""(?<id>[^""]*)"",\s*""Command"":\s*""(?<command>.*?)""\s*\}\s*,?)*\s*\]\s*\}", RegexOptions.Compiled);

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
