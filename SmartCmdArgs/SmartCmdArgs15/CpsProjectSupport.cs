using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs15
{
    public static class CpsProjectSupport
    {
        public static void SetCpsProjectArguments(EnvDTE.Project project, string arguments)
        {
            IVsBrowseObjectContext context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
            {
                // VC implements this on their DTE.Project.Object
                context = project.Object as IVsBrowseObjectContext;
            }

            if (context != null)
            {
                var launchSettingsProvider = context.UnconfiguredProject.Services.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                var activeLaunchProfile = launchSettingsProvider.ActiveProfile;

                if (activeLaunchProfile == null)
                    return;

                WritableLaunchProfile writableLaunchProfile = new WritableLaunchProfile(launchSettingsProvider.ActiveProfile);
                writableLaunchProfile.CommandLineArgs = arguments;

                // Does not work on VS2015, which should be okay ...
                // We don't hold references for VS2015, where the interface is called IThreadHandling
                IProjectThreadingService projectThreadingService = context.UnconfiguredProject.ProjectService.Services.ThreadingPolicy;
                projectThreadingService.ExecuteSynchronously(() =>
                {
                    return launchSettingsProvider.AddOrUpdateProfileAsync(writableLaunchProfile, addToFront: false);
                });
            }
        }

        public static void GetCpsProjectAllArguments(EnvDTE.Project project, List<string> allArgs)
        {
            IVsBrowseObjectContext context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
            {
                // VC implements this on their DTE.Project.Object
                context = project.Object as IVsBrowseObjectContext;
            }

            if (context != null)
            {
                var launchSettingsProvider = context.UnconfiguredProject.Services.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                var launchProfiles = launchSettingsProvider?.CurrentSnapshot?.Profiles;

                if (launchProfiles == null)
                    return;

                allArgs.AddRange(launchProfiles.Select(launchProfile => launchProfile.CommandLineArgs));
            }
        }
    }
}
