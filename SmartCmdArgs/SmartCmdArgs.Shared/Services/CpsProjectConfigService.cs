using EnvDTE;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using SmartCmdArgs.DataSerialization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks.Dataflow;

// This isolation of Microsoft.VisualStudio.ProjectSystem dependencies into one file ensures compatibility
// across various Visual Studio installations. This is crucial because not all Visual Studio workloads
// include the ManagedProjectSystem extension by default. For instance, installing Visual Studio with only
// the C++ workload does not install this extension, whereas it's included with the .NET workload at
// "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\Microsoft\ManagedProjectSystem".
// One might consider adding the Microsoft.VisualStudio.ProjectSystem assembly to the VSIX. However, this approach fails due to
// Visual Studio's configuration file (located at "%userprofile%\AppData\Local\Microsoft\VisualStudio\17.0_xxxxxxxx\devenv.exe.config")
// specifying a binding redirect for Microsoft.VisualStudio.ProjectSystem.Managed to the latest version.
// As such, ensuring compatibility requires knowledge of the version Visual Studio redirects to, which varies
// by Visual Studio installation version.

namespace SmartCmdArgs.Services
{
    public interface ICpsProjectConfigService
    {
        string GetActiveLaunchProfileName(Project project);
        void GetItemsFromConfig(Project project, List<CmdItemJson> allArgs, bool includeArgs, bool includeEnvVars, bool includeWorkDir, bool includeLaunchApp);
        IEnumerable<string> GetLaunchProfileNames(Project project);
        IDisposable ListenToLaunchProfileChanges(Project project, Action listener);
        void SetActiveLaunchProfileByName(Project project, string profileName);
        void SetActiveLaunchProfileToVirtualProfile(Project project);
        void SetConfig(Project project, string arguments, IDictionary<string, string> envVars, string workDir, string launchApp);
    }

    public class CpsProjectConfigService : ICpsProjectConfigService
    {
        public static string VirtualProfileName = "Smart CLI Args";

        private readonly IOptionsSettingsService optionsSettingsService;

        public CpsProjectConfigService(
            IOptionsSettingsService optionsSettingsService)
        {
            this.optionsSettingsService = optionsSettingsService;
        }

        private bool TryGetProjectServices(EnvDTE.Project project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices)
        {
            IVsBrowseObjectContext context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
            {
                // VC implements this on their DTE.Project.Object
                context = project.Object as IVsBrowseObjectContext;
            }

            if (context == null)
            {
                unconfiguredProjectServices = null;
                projectServices = null;

                return false;
            }
            else
            {
                UnconfiguredProject unconfiguredProject = context.UnconfiguredProject;

                // VS2017 returns the interface types of the services classes but VS2019 returns the classes directly.
                // Hence, we need to obtain the object via reflection to avoid MissingMethodExceptions.
                object services = typeof(UnconfiguredProject).GetProperty("Services").GetValue(unconfiguredProject);
                object prjServices = typeof(IProjectService).GetProperty("Services").GetValue(unconfiguredProject.ProjectService);

                unconfiguredProjectServices = services as IUnconfiguredProjectServices;
                projectServices = prjServices as IProjectServices;

                return unconfiguredProjectServices != null && project != null;
            }
        }

        public string GetActiveLaunchProfileName(EnvDTE.Project project)
        {
            if (TryGetProjectServices(project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                return launchSettingsProvider?.ActiveProfile?.Name;
            }
            return null;
        }

        public void SetActiveLaunchProfileByName(EnvDTE.Project project, string profileName)
        {
            if (TryGetProjectServices(project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                projectServices.ThreadingPolicy.ExecuteSynchronously(async () =>
                {
                    await launchSettingsProvider.SetActiveProfileAsync(profileName);
                });
            }
        }

        public void SetActiveLaunchProfileToVirtualProfile(EnvDTE.Project project) => SetActiveLaunchProfileByName(project, VirtualProfileName);

        public IEnumerable<string> GetLaunchProfileNames(EnvDTE.Project project)
        {
            if (TryGetProjectServices(project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                return launchSettingsProvider?.CurrentSnapshot?.Profiles?.Select(p => p.Name);
            }
            return null;
        }

        public IDisposable ListenToLaunchProfileChanges(EnvDTE.Project project, Action listener)
        {
            if (TryGetProjectServices(project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();

                if (launchSettingsProvider == null)
                    return null;

                return launchSettingsProvider.SourceBlock.LinkTo(
                    new ActionBlock<ILaunchSettings>(_ => listener()),
                    new DataflowLinkOptions { PropagateCompletion = true });
            }

            return null;
        }

        public void SetConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars, string workDir, string launchApp)
        {
            IUnconfiguredProjectServices unconfiguredProjectServices;
            IProjectServices projectServices;

            if (TryGetProjectServices(project, out unconfiguredProjectServices, out projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();

                ILaunchProfile baseLaunchProfile = null;
                if (optionsSettingsService.UseCpsVirtualProfile)
                {
                    baseLaunchProfile = launchSettingsProvider.CurrentSnapshot.Profiles.FirstOrDefault(x => x.Name == VirtualProfileName);
                }

                if (baseLaunchProfile == null)
                {
                    baseLaunchProfile = launchSettingsProvider?.ActiveProfile;
                }

                if (baseLaunchProfile == null)
                    return;

                WritableLaunchProfile writableLaunchProfile = new WritableLaunchProfile(baseLaunchProfile);

                if (arguments != null)
                    writableLaunchProfile.CommandLineArgs = arguments;

                if (envVars != null)
                    writableLaunchProfile.EnvironmentVariables = envVars.ToImmutableDictionary();

                if (workDir != null)
                    writableLaunchProfile.WorkingDirectory = workDir;

                if (launchApp != null)
                    writableLaunchProfile.CommandName = launchApp;

                if (optionsSettingsService.UseCpsVirtualProfile)
                {
                    writableLaunchProfile.Name = VirtualProfileName;
                    writableLaunchProfile.DoNotPersist = true;
                }

                projectServices.ThreadingPolicy.ExecuteSynchronously(() =>
                {
                    return launchSettingsProvider.AddOrUpdateProfileAsync(writableLaunchProfile, addToFront: false);
                });
            }
        }

        public void GetItemsFromConfig(EnvDTE.Project project, List<CmdItemJson> allArgs, bool includeArgs, bool includeEnvVars, bool includeWorkDir, bool includeLaunchApp)
        {
            IUnconfiguredProjectServices unconfiguredProjectServices;
            IProjectServices projectServices;

            if (TryGetProjectServices(project, out unconfiguredProjectServices, out projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                var launchProfiles = launchSettingsProvider?.CurrentSnapshot?.Profiles;

                if (launchProfiles == null)
                    return;

                foreach (var profile in launchProfiles)
                {
                    var profileGrp = new CmdItemJson { Command = profile.Name, LaunchProfile = profile.Name, Items = new List<CmdItemJson>() };

                    if (includeArgs && !string.IsNullOrEmpty(profile.CommandLineArgs))
                    {
                        profileGrp.Items.Add(new CmdItemJson { Type = ViewModel.CmdParamType.CmdArg, Command = profile.CommandLineArgs, Enabled = true });
                    }

                    if (includeEnvVars)
                    {
                        foreach (var envVarPair in profile.EnvironmentVariables)
                        {
                            profileGrp.Items.Add(new CmdItemJson { Type = ViewModel.CmdParamType.EnvVar, Command = $"{envVarPair.Key}={envVarPair.Value}", Enabled = true });
                        }
                    }

                    if (includeWorkDir && !string.IsNullOrEmpty(profile.WorkingDirectory))
                    {
                        profileGrp.Items.Add(new CmdItemJson { Type = ViewModel.CmdParamType.WorkDir, Command = profile.WorkingDirectory, Enabled = true });
                    }

                    if (includeLaunchApp && !string.IsNullOrEmpty(profile.CommandName))
                    {
                        profileGrp.Items.Add(new CmdItemJson { Type = ViewModel.CmdParamType.LaunchApp, Command = profile.CommandName, Enabled = true });
                    }

                    if (profileGrp.Items.Count > 0)
                    {
                        allArgs.Add(profileGrp);
                    }
                }
            }
        }
    }

    class WritableLaunchProfile : ILaunchProfile
#if VS17
        , IPersistOption
#endif
    {
        // ILaunchProfile
        public string Name { set; get; }
        public string CommandName { set; get; }
        public string ExecutablePath { set; get; }
        public string CommandLineArgs { set; get; }
        public string WorkingDirectory { set; get; }
        public bool LaunchBrowser { set; get; }
        public string LaunchUrl { set; get; }
        public ImmutableDictionary<string, string> EnvironmentVariables { set; get; }
        public ImmutableDictionary<string, object> OtherSettings { set; get; }

        // IPersistOption
        public bool DoNotPersist { get; set; }

        public WritableLaunchProfile(ILaunchProfile launchProfile)
        {
            // ILaunchProfile
            Name = launchProfile.Name;
            ExecutablePath = launchProfile.ExecutablePath;
            CommandName = launchProfile.CommandName;
            CommandLineArgs = launchProfile.CommandLineArgs;
            WorkingDirectory = launchProfile.WorkingDirectory;
            LaunchBrowser = launchProfile.LaunchBrowser;
            LaunchUrl = launchProfile.LaunchUrl;
            EnvironmentVariables = launchProfile.EnvironmentVariables;
            OtherSettings = launchProfile.OtherSettings;

#if VS17
            if (launchProfile is IPersistOption persistOptionLaunchProfile)
            {
                // IPersistOption
                DoNotPersist = persistOptionLaunchProfile.DoNotPersist;
            }
#endif
        }
    }
}
