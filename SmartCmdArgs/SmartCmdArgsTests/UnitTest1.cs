using System;
using System.Collections;
using System.Text;
using System.Reflection;
using Microsoft.VsSDK.UnitTestLibrary;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmartCmdArgs;
using Microsoft.VSSDK.Tools.VsIdeTesting;
using EnvDTE;
using Microsoft.VsSDK.IntegrationTestLibrary;

namespace UnitTestProject1
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
        public void TestMethod1()
        {
            InvokeInUIThread(() => {
                IVsShell shell = GetService<SVsShell, IVsShell>();
                Assert.IsNotNull(shell);

                IVsPackage package = Utils.LoadPackage(new Guid(SmartCmdArgs.CmdArgsPackage.PackageGuidString));
            });
        }
    }
}
