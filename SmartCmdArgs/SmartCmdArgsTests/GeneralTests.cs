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
    public class GeneralTests : TestBase
    {
        public void CollectArgsFromExistingProjectConfigsTest(TestLanguage language)
        {
            var openSolutionSuccess = OpenSolutionWithName(language, "CollectArgsTest");
            Assert.That(openSolutionSuccess, Is.True);

            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;

            Assert.That(argItems, Is.Not.Null);
            if (language == TestLanguage.NodeJS)
                Assert.That(argItems.Select(item => item.Command).Distinct().Count(), Is.EqualTo(1));
            else
                Assert.That(argItems.Select(item => item.Command).Distinct().Count(), Is.GreaterThan(1));

            Assert.That(argItems, Has.All.Property("Command").StartWith("args for "));
            Assert.That(argItems, Has.All.Property("Id").Not.EqualTo(Guid.Empty));
        }

        public void CollectArgsDistinctFromExistingProjectConfigsTest(TestLanguage language)
        {
            var openSolutionSuccess = OpenSolutionWithName(language, "CollectArgsDistinctTest");
            Assert.That(openSolutionSuccess, Is.True);
            
            var package = (CmdArgsPackage)Utils.LoadPackage(new Guid(CmdArgsPackage.PackageGuidString));
            var argItems = package?.ToolWindowViewModel?.CurrentArgumentList?.DataCollection;

            Assert.That(argItems, Is.Not.Null);
            Assert.That(argItems, Has.Count.EqualTo(1));

            var argItem = argItems[0];
            Assert.That(argItem, Has.Property("Id").Not.EqualTo(Guid.Empty));
            Assert.That(argItem, Has.Property("Command").EqualTo("same args in every config"));
        }

        public void AddNewArgLineViaCommandTest(TestLanguage language)
        {
            var openSolutionSuccess = OpenSolutionWithName(language, "DefaultProject");
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

