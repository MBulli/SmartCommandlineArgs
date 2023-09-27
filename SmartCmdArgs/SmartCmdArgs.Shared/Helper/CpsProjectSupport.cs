﻿using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using SmartCmdArgs.DataSerialization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks.Dataflow;

namespace SmartCmdArgs.Helper
{
    public static class CpsProjectSupport
    {
        private static bool TryGetProjectServices(EnvDTE.Project project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices)
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

        public static string GetActiveLaunchProfileName(EnvDTE.Project project)
        {
            if (TryGetProjectServices(project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                return launchSettingsProvider?.ActiveProfile?.Name;
            }
            return null;
        }

        public static IEnumerable<string> GetLaunchProfileNames(EnvDTE.Project project)
        {
            if (TryGetProjectServices(project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                return launchSettingsProvider?.CurrentSnapshot?.Profiles?.Select(p => p.Name);
            }
            return null;
        }

        public static IDisposable ListenToLaunchProfileChanges(EnvDTE.Project project, Action<ILaunchSettings> listener)
        {
            if (TryGetProjectServices(project, out IUnconfiguredProjectServices unconfiguredProjectServices, out IProjectServices projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();

                if (launchSettingsProvider == null)
                    return null;

                return launchSettingsProvider.SourceBlock.LinkTo(
                    new ActionBlock<ILaunchSettings>(listener),
                    new DataflowLinkOptions { PropagateCompletion = true });
            }

            return null;
        }

        public static void SetCpsProjectConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars, string workDir)
        {
            IUnconfiguredProjectServices unconfiguredProjectServices;
            IProjectServices projectServices;

            if (TryGetProjectServices(project, out unconfiguredProjectServices, out projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                var activeLaunchProfile = launchSettingsProvider?.ActiveProfile;

                if (activeLaunchProfile == null)
                    return;

                WritableLaunchProfile writableLaunchProfile = new WritableLaunchProfile(activeLaunchProfile);

                if (arguments != null)
                    writableLaunchProfile.CommandLineArgs = arguments;
                
                if (envVars != null)
                    writableLaunchProfile.EnvironmentVariables = envVars.ToImmutableDictionary();

                if (workDir != null)
                    writableLaunchProfile.WorkingDirectory = workDir;

                // Does not work on VS2015, which should be okay ...
                // We don't hold references for VS2015, where the interface is called IThreadHandling
                IProjectThreadingService projectThreadingService = projectServices.ThreadingPolicy;
                projectThreadingService.ExecuteSynchronously(() =>
                {
                    return launchSettingsProvider.AddOrUpdateProfileAsync(writableLaunchProfile, addToFront: false);
                });
            }
        }

        public static List<CmdArgumentJson> GetCpsProjectAllArguments(EnvDTE.Project project, bool includeArgs, bool includeEnvVars, bool includeWorkDir)
        {
            IUnconfiguredProjectServices unconfiguredProjectServices;
            IProjectServices projectServices;

            var result = new List<CmdArgumentJson>();

            if (TryGetProjectServices(project, out unconfiguredProjectServices, out projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                var launchProfiles = launchSettingsProvider?.CurrentSnapshot?.Profiles;

                if (launchProfiles == null)
                    return result;

                foreach (var profile in launchProfiles)
                {
                    var profileGrp = new CmdArgumentJson { Command = profile.Name, LaunchProfile = profile.Name, Items = new List<CmdArgumentJson>() };

                    if (includeArgs && !string.IsNullOrEmpty(profile.CommandLineArgs))
                    {
                        profileGrp.Items.Add(new CmdArgumentJson { Type = ViewModel.CmdParamType.CmdArg, Command = profile.CommandLineArgs, Enabled = true });
                    }

                    if (includeEnvVars)
                    {
                        foreach (var envVarPair in profile.EnvironmentVariables)
                        {
                            profileGrp.Items.Add(new CmdArgumentJson { Type = ViewModel.CmdParamType.EnvVar, Command = $"{envVarPair.Key}={envVarPair.Value}", Enabled = true });
                        }
                    }

                    if (includeWorkDir && !string.IsNullOrEmpty(profile.WorkingDirectory))
                    {
                        profileGrp.Items.Add(new CmdArgumentJson { Type = ViewModel.CmdParamType.WorkDir, Command = profile.WorkingDirectory, Enabled = true });
                    }

                    if (profileGrp.Items.Count > 0)
                    {
                        result.Add(profileGrp);
                    }
                }
            }

            return result;
        }
    }

    class WritableLaunchProfile : ILaunchProfile
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

        public WritableLaunchProfile(ILaunchProfile launchProfile)
        {
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
    }
}
