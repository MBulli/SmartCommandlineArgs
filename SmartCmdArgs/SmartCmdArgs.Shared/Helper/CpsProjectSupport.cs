using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static void SetCpsProjectArguments(EnvDTE.Project project, string arguments, bool cpsUseCustomProfile)
        {
            IUnconfiguredProjectServices unconfiguredProjectServices;
            IProjectServices projectServices;

            if (TryGetProjectServices(project, out unconfiguredProjectServices, out projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                var activeLaunchProfile = launchSettingsProvider?.ActiveProfile;

                if (activeLaunchProfile == null)
                    return;

                WritableLaunchProfile writableLaunchProfile = new WritableLaunchProfile(activeLaunchProfile,cpsUseCustomProfile);
                writableLaunchProfile.CommandLineArgs = arguments;
				
                // Does not work on VS2015, which should be okay ...
                // We don't hold references for VS2015, where the interface is called IThreadHandling
                IProjectThreadingService projectThreadingService = projectServices.ThreadingPolicy;
                projectThreadingService.ExecuteSynchronously(() =>
                {
					var tsk = launchSettingsProvider.AddOrUpdateProfileAsync(writableLaunchProfile, addToFront: false);
					if (cpsUseCustomProfile && IsVeryFirstRun) {
						tsk = tsk.ContinueWith(_ => launchSettingsProvider.SetActiveProfileAsync(WritableLaunchProfile.CustomProfileName));
					IsVeryFirstRun = false;
					}
					
                    return tsk;
                });
            }
        }
		private static bool IsVeryFirstRun=true;
        public static Dictionary<string, string> GetCpsProjectAllArguments(EnvDTE.Project project)
        {
            IUnconfiguredProjectServices unconfiguredProjectServices;
            IProjectServices projectServices;

            var result = new Dictionary<string, string>();

            if (TryGetProjectServices(project, out unconfiguredProjectServices, out projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                var launchProfiles = launchSettingsProvider?.CurrentSnapshot?.Profiles;

                if (launchProfiles == null)
                    return result;

                foreach (var profile in launchProfiles)
                {
                    if (!string.IsNullOrEmpty(profile.CommandLineArgs))
                        result.Add(profile.Name, profile.CommandLineArgs);
                }
            }

            return result;
        }
    }

    class WritableLaunchProfile : ILaunchProfile
#if VS17
		, Microsoft.VisualStudio.ProjectSystem.Debug.IPersistOption
#endif
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
		public bool DoNotPersist { get; set; }

		public const string CustomProfileName = "Smart CLI Args";
		public WritableLaunchProfile(ILaunchProfile launchProfile, bool cpsUseCustomProfile)
        {
			if (!cpsUseCustomProfile)
				Name = launchProfile.Name;
			else {
				Name = CustomProfileName;
				DoNotPersist = true;
			}
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
