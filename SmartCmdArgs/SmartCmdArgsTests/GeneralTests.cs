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
using NUnit.Framework;
using SmartCmdArgs;
using Assert = NUnit.Framework.Assert;

namespace SmartCmdArgsTests
{
    [TestClass]
    public class GeneralTests : TestBase
    {
        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void CollectArgsFromExistingProjectConfigsTest()
        {
            var openSolutionSuccess = OpenSolutionWithName("CollectArgsTest");
            Assert.That(openSolutionSuccess, Is.True);

            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;

            Assert.That(argItems, Is.Not.Null);
            if (CurrentLanguage == TestLanguages.NodeJS)
                Assert.That(argItems.Select(item => item.Command).Distinct().Count(), Is.EqualTo(1));
            else
                Assert.That(argItems.Select(item => item.Command).Distinct().Count(), Is.GreaterThan(1));

            Assert.That(argItems, Has.All.Property("Command").StartWith("args for "));
            Assert.That(argItems, Has.All.Property("Id").Not.EqualTo(Guid.Empty));
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        [DeploymentItem("ConsoleApplicationVC.zip")]
        public void CollectArgsDistinctFromExistingProjectConfigsTest()
        {
            var openSolutionSuccess = OpenSolutionWithName("CollectArgsDistinctTest");
            Assert.That(openSolutionSuccess, Is.True);
            
            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;

            Assert.That(argItems, Is.Not.Null);
            Assert.That(argItems, Has.Count.EqualTo(1));

            var argItem = argItems[0];
            Assert.That(argItem, Has.Property("Id").Not.EqualTo(Guid.Empty));
            Assert.That(argItem, Has.Property("Command").EqualTo("same args in every config"));
        }

        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void AddNewArgLineViaCommandTest()
        {
            var openSolutionSuccess = OpenSolutionWithName("DefaultProject");
            Assert.That(openSolutionSuccess, Is.True);

            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));

            ICommand addCommand = package?.ToolWindowViewModel?.AddEntryCommand;
            Assert.That(addCommand, Is.Not.Null);

            InvokeInUIThread(() =>
            {
                Assert.That(package.ToolWindowViewModel.AddEntryCommand.CanExecute(null), Is.True);
                addCommand.Execute(null);
            });

            var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;
            Assert.That(argItems, Is.Not.Null);

            Assert.That(argItems, Has.Count.EqualTo(1));

            var argItem = argItems[0];
            Assert.That(argItem, Has.Property("Id").Not.EqualTo(Guid.Empty));
            Assert.That(argItem, Has.Property("Command").EqualTo(""));
        }
    }
}

