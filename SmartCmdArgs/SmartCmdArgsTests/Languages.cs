using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VSSDK.Tools.VsIdeTesting;

using static Microsoft.VSSDK.Tools.VsIdeTesting.VsIdeTestHostContants.TestPropertyName;
using static Microsoft.VSSDK.Tools.VsIdeTesting.VsIdeTestHostContants.HostRestartOptions;

namespace SmartCmdArgsTests
{
    public abstract class LanguagesAgnostic : TestBase
    {
        protected TestLanguage Language = TestLanguage.CSharpDotNetFW;

        public LanguagesAgnostic(TestLanguage language)
        {
            Language = language;
        }

        #region set cmd args tests
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void SetCommandLineArgsViaDebug() => RunTest<SetCmdArgsTests>(_ => _.SetCommandLineArgsViaDebug(Language));
        #endregion

        #region General tests
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void CollectArgsFromExistingProjectConfigsTest() => RunTest<GeneralTests>(_ => _.CollectArgsFromExistingProjectConfigsTest(Language));

        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        [DeploymentItem("ConsoleApplicationVC.zip")]
        public void CollectArgsDistinctFromExistingProjectConfigsTest() => RunTest<GeneralTests>(_ => _.CollectArgsDistinctFromExistingProjectConfigsTest(Language));

        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void AddNewArgLineViaCommandTest() => RunTest<GeneralTests>(_ => _.AddNewArgLineViaCommandTest(Language));
        #endregion

        #region Suo saving tests
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void SaveAndLoadSuoTest() => RunTest<SuoSavingTests>(_ => _.SaveAndLoadSuoTest(Language));
        #endregion

        #region VCS support tests
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void SaveCommandsToJsonTest() => RunTest<VcsSupportTests>(_ => _.SaveCommandsToJsonTest(Language));
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void LoadCommandsFromExistingJsonTest() => RunTest<VcsSupportTests>(_ => _.LoadCommandsFromExistingJsonTest(Language));
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void LoadCommandsFromCreatedJsonTest() => RunTest<VcsSupportTests>(_ => _.LoadCommandsFromCreatedJsonTest(Language));
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void LoadChangesFromJsonTest() => RunTest<VcsSupportTests>(_ => _.LoadChangesFromJsonTest(Language));
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void AvoidEmptyJsonFileTest() => RunTest<VcsSupportTests>(_ => _.AvoidEmptyJsonFileTest(Language));
        [TestMethod, HostType("VS IDE")]
        [TestProperty(RegistryHiveName, HiveName)]
        [TestProperty(HiveStartFlags, HiveStartArgs)]
        [TestProperty(RestartOptions, Before)]
        public void DisabledVcsSupportTest() => RunTest<VcsSupportTests>(_ => _.DisabledVcsSupportTest(Language));
        #endregion
    }


    [TestClass]
    public class CSharp : LanguagesAgnostic
    {
        public CSharp() : base(TestLanguage.CSharpDotNetFW) { }
    }

    [TestClass]
    public class CSharp_Core : LanguagesAgnostic
    {
        public CSharp_Core() : base(TestLanguage.CSharpDotNetCore) { }
    }

    [TestClass]
    public class Cpp : LanguagesAgnostic
    {
        public Cpp() : base(TestLanguage.CPP) { }
    }

    [TestClass]
    public class VbDotNet : LanguagesAgnostic
    {
        public VbDotNet() : base(TestLanguage.VBDotNetFW) { }
    }

    [TestClass]
    public class NodeJs : LanguagesAgnostic
    {
        public NodeJs() : base(TestLanguage.NodeJS) { }
    }

    [TestClass]
    public class FSharp : LanguagesAgnostic
    {
        public FSharp() : base(TestLanguage.FSharpDotNetFW) { }
    }
}
