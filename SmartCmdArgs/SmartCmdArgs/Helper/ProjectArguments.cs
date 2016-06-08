using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace SmartCmdArgs.Helper
{
    public static class ProjectArguments
    {
        private class ProjectArgumentsHandlers
        {
            public delegate void SetArgumentsDelegate(EnvDTE.Project project, string arguments);
            public delegate void GetAllArgumentsDelegate(EnvDTE.Project project, List<string> allArgs);
            public SetArgumentsDelegate SetArguments;
            public GetAllArgumentsDelegate GetAllArguments;
        }

        private static void SetSingleConfigArgument(EnvDTE.Project project, string arguments, string propertyName)
        {
            try {project.Properties.Item(propertyName).Value = arguments; }
            catch { Debug.WriteLine("Could not set arguments for project: " + project.UniqueName); }
        }

        private static void GetSingleConfigAllArguments(EnvDTE.Project project, List<string> allArgs, string propertyName)
        {
            try
            {
                string cmdarg = project.Properties.Item(propertyName).Value as string;
                if (!string.IsNullOrEmpty(cmdarg))
                {
                    allArgs.Add(cmdarg);
                }
            }
            catch { Debug.WriteLine("Could not get arguments for project: " + project.UniqueName); }
        }

        private static void SetMultiConfigArguments(EnvDTE.Project project, string arguments, string propertyName)
        {
            // Set the arguments only on the active configuration
            EnvDTE.Properties properties = project.ConfigurationManager?.ActiveConfiguration?.Properties;
            try { properties.Item(propertyName).Value = arguments; }
            catch { Debug.WriteLine("Could not set arguments for project: " + project.UniqueName); }
        }

        private static void GetMultiConfigAllArguments(EnvDTE.Project project, List<string> allArgs, string propertyName)
        {
            // Read properties for all configurations (e.g. Debug/Release)
            foreach (EnvDTE.Configuration config in project.ConfigurationManager)
            {
                try {
                    string cmdarg = config.Properties.Item(propertyName).Value as string;
                    if (!string.IsNullOrEmpty(cmdarg))
                    {
                        allArgs.Add(cmdarg);
                    }
                }
                catch { Debug.WriteLine("Could not get arguments for project: " + project.UniqueName); }
            }
        }

        private static Dictionary<string, ProjectArgumentsHandlers> supportedProjects = new Dictionary<string, ProjectArgumentsHandlers>()
        {
            // C#
            {"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetMultiConfigArguments(project, arguments, "StartArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "StartArguments")
            } },
            // VB.NET
            {"{F184B08F-C81C-45F6-A57F-5ABD9991F28F}", new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetMultiConfigArguments(project, arguments, "CommandArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "CommandArguments")
            } },
            // C/C++
            {"{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetMultiConfigArguments(project, arguments, "CommandArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "CommandArguments")
            } },
            // Python
            {"{888888a0-9f3d-457c-b088-3a5042f75d52}", new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetSingleConfigArgument(project, arguments, "CommandLineArguments"),
                GetAllArguments = (project, allArgs) => GetSingleConfigAllArguments(project, allArgs, "CommandLineArguments")
            } },
        };

        public static bool IsSupportedProject(EnvDTE.Project project)
        {
            return supportedProjects.ContainsKey(project.Kind);
        }

        public static void AddAllArguments(EnvDTE.Project project, List<string> allArgs)
        {
            ProjectArgumentsHandlers handler;
            if (supportedProjects.TryGetValue(project.Kind, out handler))
            {
                handler.GetAllArguments(project, allArgs);
            }
        }

        public static void SetArguments(EnvDTE.Project project, string arguments)
        {
            ProjectArgumentsHandlers handler;
            if (supportedProjects.TryGetValue(project.Kind, out handler))
            {
                handler.SetArguments(project, arguments);
            }
        }
    }
}
