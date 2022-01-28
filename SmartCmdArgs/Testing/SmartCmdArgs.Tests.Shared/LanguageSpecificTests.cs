using System.Threading.Tasks;
using System.Linq;

using System;
using Xunit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;
using System.IO;

namespace SmartCmdArgs.Tests.LanguageSpecificTests
{
    public abstract class LanguageSpecificTests : TestBase
    {
        private TestLanguage _language;

        public LanguageSpecificTests(TestLanguage language)
        {
            _language = language;
        }

        [VsFact]
        public async Task CollectArgsFromExistingProjectConfigsTest()
        {
            await OpenSolutionWithNameAsync(_language, "CollectArgsTest");

            var package = await LoadExtensionAsync();
            Assert.False(package?.ToolWindowViewModel?.SettingsViewModel?.VcsSupportEnabled, "VCS support must be disabled");

            var args = package?.ToolWindowViewModel?.TreeViewModel?.AllArguments?.ToList();

            Assert.NotNull(args);

            Assert.NotEmpty(args);

            Assert.All(args, x => Assert.StartsWith("args for ", x.Value));
            Assert.All(args, x => Assert.NotEqual(Guid.Empty, x.Id));
        }
    }

    public class CSharp : LanguageSpecificTests
    {
        public CSharp() : base(TestLanguage.CSharpDotNetFW) { }
    }

    public class CSharp_Core : LanguageSpecificTests
    {
        public CSharp_Core() : base(TestLanguage.CSharpDotNetCore) { }
    }

    public class Cpp : LanguageSpecificTests
    {
        public Cpp() : base(TestLanguage.CPP) { }
    }

    public class VbDotNet : LanguageSpecificTests
    {
        public VbDotNet() : base(TestLanguage.VBDotNetFW) { }
    }

    public class NodeJs : LanguageSpecificTests
    {
        public NodeJs() : base(TestLanguage.NodeJS) { }
    }

    public class FSharp : LanguageSpecificTests
    {
        public FSharp() : base(TestLanguage.FSharpDotNetFW) { }
    }
}