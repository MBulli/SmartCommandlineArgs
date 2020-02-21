using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Debug;

namespace SmartCmdArgs15
{
    class WritableLaunchProfile : ILaunchProfile, IPersistOption
    {
        public string Name { set; get; }
        public string CommandName { set; get; }
        public string ExecutablePath { set; get; }
        public string CommandLineArgs { set; get; }
        public string WorkingDirectory { set; get; }
        public bool LaunchBrowser { set; get; }
        public string LaunchUrl { set; get; }
        public ImmutableDictionary<string, string> EnvironmentVariables { set; get; }
        public ImmutableDictionary<string, object> OtherSettings { set; get; }

        public WritableLaunchProfile(ILaunchProfile launchProfile, bool doNotPersist)
        {
            DoNotPersist = doNotPersist;
            Name = launchProfile.Name;
            ExecutablePath = launchProfile.ExecutablePath;
            CommandName = launchProfile.CommandName;
            CommandLineArgs = launchProfile.CommandLineArgs;
            WorkingDirectory = launchProfile.WorkingDirectory;
            LaunchBrowser = launchProfile.LaunchBrowser;
            LaunchUrl = launchProfile.LaunchUrl;
            EnvironmentVariables = launchProfile.EnvironmentVariables;
            OtherSettings = launchProfile.OtherSettings;
        }

        public bool DoNotPersist { get; }
    }
}
