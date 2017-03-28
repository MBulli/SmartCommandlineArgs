using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VSSDK.Tools.VsIdeTesting;
using NUnit.Framework;
using SmartCmdArgsTests.Utils;
using Assert = NUnit.Framework.Assert;

namespace SmartCmdArgsTests
{
    [TestClass]
    public class SetCmdArgsTests : TestBase
    {
        [TestMethod]
        [HostType("VS IDE")]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RegistryHiveName, HiveName)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.HiveStartFlags, HiveStartArgs)]
        [TestProperty(VsIdeTestHostContants.TestPropertyName.RestartOptions, VsIdeTestHostContants.HostRestartOptions.Before)]
        public void SetCommandLineArgsViaDebug()
        {
            OpenSolutionWithName("ReadCmdArgsProject");
            LoadExtension();

            var args = new List<(string Arg, bool Enabled)>
            {
                ("Arg1", true),
                ("Arg42", false),
                ("Arg2", true),
            };

            var curList = ExtensionPackage.ToolWindowViewModel.CurrentArgumentList;
            InvokeInUIThread(() =>
            {
                foreach (var arg in args)
                {
                    curList.AddNewItem(arg.Arg, arg.Enabled);
                }
            });

            Dte.Solution.SolutionBuild.Build(true);
            using (var waiter = new WaitUntil())
            {
                Dte.Events.DebuggerEvents.OnEnterDesignMode += reason => waiter.Finish();
                Dte.ExecuteCommand("Debug.Start");
            }

            Assert.That(File.Exists("ReadCmdArgsProject/CmdLineArgs.txt"), Is.True);

            var cmdArgsFile = File.ReadAllText("ReadCmdArgsProject/CmdLineArgs.txt");
            var cmdArgsTest = string.Join(" ", args.Where(arg => arg.Enabled).Select(arg => arg.Arg));
            Assert.That(cmdArgsFile, Is.EqualTo(cmdArgsTest));
        }
    }
}
