using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using SmartCmdArgsTests.Utils;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace SmartCmdArgsTests
{
    public class SetCmdArgsTests : TestBase
    {
        public void SetCommandLineArgsViaDebug(TestLanguage language)
        {
            OpenSolutionWithName(language, "ReadCmdArgsProject");
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


        public void SetCommandLineArgsViaResart(TestLanguage language)
        {
            OpenSolutionWithName(language, "ReadCmdArgsProject");
            LoadExtension();

            switch (language)
            {
                case TestLanguage.CSharpDotNetFW:
                    Assert.True(SetBreakpoint("Program.cs", 14));
                    break;
                case TestLanguage.CSharpDotNetCore:
                    Assert.True(SetBreakpoint("Program.cs", 10));
                    break;
                case TestLanguage.CPP:
                    Assert.True(SetBreakpoint("Project1.cpp", 10));
                    break;
                case TestLanguage.VBDotNetFW:
                    Assert.True(SetBreakpoint("Module1.vb", 3));
                    break;
                case TestLanguage.NodeJS:
                    Assert.True(SetBreakpoint("app.ts", 9));
                    break;
                case TestLanguage.FSharpDotNetFW:
                    Assert.True(SetBreakpoint("Program.fs", 6));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(language), language, null);
            }

            bool SetBreakpoint(string fileName, int line, int column = 1)
            {
                ProjectItem codeFile = Dte.Solution.FindProjectItem(fileName);
                if (codeFile == null)
                    return false;

                Dte.Debugger.Breakpoints.Add("", codeFile.FileNames[0], line, column);
                return true;
            }

            using (var waiter = new WaitUntil())
            {
                Dte.Events.DebuggerEvents.OnEnterBreakMode +=
                    (dbgEventReason reason, ref dbgExecutionAction action) => waiter.Finish();
                Dte.Debugger.Go();
            }

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

            Dte.Debugger.Breakpoints.Item(1).Enabled = false;

            using (var waiter = new WaitUntil())
            {
                int dbgDesignEnters = 0;
                Dte.Events.DebuggerEvents.OnEnterDesignMode += reason =>
                {
                    dbgDesignEnters++;
                    if (dbgDesignEnters == 2) waiter.Finish();
                };
                Dte.ExecuteCommand("Debug.Restart");
            }

            Assert.That(File.Exists("ReadCmdArgsProject/CmdLineArgs.txt"), Is.True);

            var cmdArgsFile = File.ReadAllText("ReadCmdArgsProject/CmdLineArgs.txt");
            var cmdArgsTest = string.Join(" ", args.Where(arg => arg.Enabled).Select(arg => arg.Arg));
            Assert.That(cmdArgsFile, Is.EqualTo(cmdArgsTest));
        }
    }
}
