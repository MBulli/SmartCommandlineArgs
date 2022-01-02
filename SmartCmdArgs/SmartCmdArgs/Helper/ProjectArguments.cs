using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.Logic;

namespace SmartCmdArgs.Helper
{
    public static class ProjectArguments
    {
        private class ProjectArgumentsHandlers
        {
            public delegate void SetArgumentsDelegate(EnvDTE.Project project, string arguments);
            public delegate void GetAllArgumentsDelegate(EnvDTE.Project project, List<CmdArgumentJson> allArgs);
            public SetArgumentsDelegate SetArguments;
            public GetAllArgumentsDelegate GetAllArguments;
        }

        private static void SetSingleConfigArgument(EnvDTE.Project project, string arguments, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try { project.Properties.Item(propertyName).Value = arguments; }
            catch (Exception ex) { Logger.Error($"Failed to set single config arguments for project '{project.UniqueName}' with error '{ex}'"); }
        }

        private static void GetSingleConfigAllArguments(EnvDTE.Project project, List<CmdArgumentJson> allArgs, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                string cmdarg = project.Properties.Item(propertyName).Value as string;
                if (!string.IsNullOrEmpty(cmdarg))
                {
                    allArgs.Add(new CmdArgumentJson { Command = cmdarg, Enabled = true });
                }
            }
            catch (Exception ex) { Logger.Error($"Failed to get single config arguments for project '{project.UniqueName}' with error '{ex}'"); }
        }

        private static void SetMultiConfigArguments(EnvDTE.Project project, string arguments, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Set the arguments only on the active configuration
            EnvDTE.Properties properties = project.ConfigurationManager?.ActiveConfiguration?.Properties;
            try { properties.Item(propertyName).Value = arguments; }
            catch (Exception ex) { Logger.Error($"Failed to set multi config arguments for project '{project.UniqueName}' with error '{ex}'"); }
        }

        private static void GetMultiConfigAllArguments(EnvDTE.Project project, List<CmdArgumentJson> allArgs, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Read properties for all configurations (e.g. Debug/Release)
            foreach (EnvDTE.Configuration config in project.ConfigurationManager)
            {
                try
                {
                    string cmdarg = config.Properties.Item(propertyName).Value as string;
                    if (!string.IsNullOrEmpty(cmdarg))
                    {
                        var configGrp = new CmdArgumentJson { Command = config.ConfigurationName, ProjectConfig = config.ConfigurationName, Items = new List<CmdArgumentJson>() };
                        configGrp.Items.Add(new CmdArgumentJson { Command = cmdarg, Enabled = true });
                        allArgs.Add(configGrp);
                    }
                }
                catch (Exception ex) { Logger.Error($"Failed to get multi config arguments for project '{project.UniqueName}' with error '{ex}'"); }
            }
        }

        private static void SetVCProjEngineArguments(EnvDTE.Project project, string arguments)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Use late binding to support VS2015 and VS2017
            dynamic vcPrj = (dynamic)project.Object; // is VCProject
            dynamic vcCfg = vcPrj?.ActiveConfiguration; // is VCConfiguration
            dynamic vcDbg = vcCfg?.DebugSettings;  // is VCDebugSettings

            // apply it first using the old way, in case the new way doesn't work for this type of projects (platforms other than Windows, for example)
            if (vcDbg != null)
            {
                vcDbg.CommandArguments = arguments;
            }
            else { Logger.Warn("SetVCProjEngineArguments: VCProject?.ActiveConfiguration?.DebugSettings returned null"); }


            if (vcCfg != null)
            {
                dynamic windowsLocalDebugger = vcCfg.Rules.Item("WindowsLocalDebugger"); // is IVCRulePropertyStorage
                if (windowsLocalDebugger != null)
                {
                    windowsLocalDebugger.SetPropertyValue("LocalDebuggerCommandArguments", arguments);
                }
                else { Logger.Warn("SetVCProjEngineArguments: ProjectConfig Rule 'WindowsLocalDebugger' returned null"); }

                dynamic windowsRemoteDebugger = vcCfg.Rules.Item("WindowsRemoteDebugger"); // is IVCRulePropertyStorage
                if (windowsRemoteDebugger != null)
                {
                    windowsRemoteDebugger.SetPropertyValue("RemoteDebuggerCommandArguments", arguments);
                }
                else 
                {
                    dynamic linuxRemoteDebugger = vcCfg.Rules.Item("LinuxWSLDebugger"); // is IVCRulePropertyStorage
                    if (linuxRemoteDebugger != null)
                    {
                        linuxRemoteDebugger.SetPropertyValue("RemoteDebuggerCommandArguments", arguments);
                    }
                    else
                    {
                        Logger.Warn("SetVCProjEngineArguments: ProjectConfig Rule 'RemoteDebuggerCommandArguments' returned null");
                    }
                }

                dynamic googleAndroidDebugger = vcCfg.Rules.Item( "GoogleAndroidDebugger" ); // is IVCRulePropertyStorage
                if( googleAndroidDebugger != null )
                {
                    googleAndroidDebugger.SetPropertyValue( "LaunchFlags", arguments );
                }              
            }
            else { Logger.Warn("SetVCProjEngineArguments: VCProject?.ActiveConfiguration? returned null"); }
        }

        private static void GetVCProjEngineAllArguments(EnvDTE.Project project, List<CmdArgumentJson> allArgs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic vcPrj = (dynamic)project.Object; // is VCProject
            dynamic configs = vcPrj?.Configurations;  // is IVCCollection

            if (configs == null)
            {
                Logger.Warn("GetVCProjEngineAllArguments: VCProject.Configurations is null");
                return;
            }

            for (int index = 1; index <= configs.Count; index++)
            {
                dynamic cfg = configs.Item(index); // is VCConfiguration
                dynamic dbg = cfg.DebugSettings;  // is VCDebugSettings

                var items = new List<string>();

                if (!string.IsNullOrEmpty(dbg?.CommandArguments))
                {
                    items.Add(dbg.CommandArguments);
                }

                // Read local debugger values
                dynamic windowsLocalDebugger = cfg.Rules.Item("WindowsLocalDebugger"); // is IVCRulePropertyStorage
                if (windowsLocalDebugger != null)
                {
                    var localArguments = windowsLocalDebugger.GetUnevaluatedPropertyValue("LocalDebuggerCommandArguments");
                    if (!string.IsNullOrEmpty(localArguments))
                    {
                        items.Add(localArguments);
                    }
                }
                else { Logger.Warn("GetVCProjEngineAllArguments: ProjectConfig Rule 'WindowsLocalDebugger' returned null"); }

                // Read remote debugger values
                dynamic windowsRemoteDebugger = cfg.Rules.Item("WindowsRemoteDebugger"); // is IVCRulePropertyStorage
                if (windowsRemoteDebugger != null)
                {
                    var remoteArguments = windowsRemoteDebugger.GetUnevaluatedPropertyValue("RemoteDebuggerCommandArguments");
                    if (!string.IsNullOrEmpty(remoteArguments))
                    {
                        items.Add(remoteArguments);
                    }
                }
                else //check WSL remote debugger
                {
                    dynamic linuxWSLDebugger = cfg.Rules.Item("LinuxWSLDebugger"); // is IVCRulePropertyStorage
                    if (linuxWSLDebugger != null)
                    {
                        var remoteArguments = windowsRemoteDebugger.GetUnevaluatedPropertyValue("RemoteDebuggerCommandArguments");
                        if (!string.IsNullOrEmpty(remoteArguments))
                        {
                            items.Add(remoteArguments);
                        }
                    }
                    else { Logger.Warn("GetVCProjEngineAllArguments: ProjectConfig Rule 'WindowsRemoteDebugger' returned null"); }
                }

                if (items.Count > 0)
                {
                    var configGrp = new CmdArgumentJson { Command = cfg.ConfigurationName, ProjectConfig = cfg.ConfigurationName, Items = new List<CmdArgumentJson>() };

                    configGrp.Items.AddRange(items.Distinct().Select(arg => new CmdArgumentJson { Command = arg, Enabled = true }));

                    allArgs.Add(configGrp);
                }
            }
        }

        private static void SetCpsProjectArguments(EnvDTE.Project project, string arguments)
        {
            // Should only be called in VS 2017 or higher
            // .Net Core 2 is not supported by VS 2015, so this should not cause problems
            SmartCmdArgs15.CpsProjectSupport.SetCpsProjectArguments(project, arguments);
        }

        private static void GetCpsProjectAllArguments(EnvDTE.Project project, List<CmdArgumentJson> allArgs)
        {
            // Should only be called in VS 2017 or higher
            // see SetCpsProjectArguments
            var profileArgsMap = SmartCmdArgs15.CpsProjectSupport.GetCpsProjectAllArguments(project);

            var profileGrps = profileArgsMap.Select(x => {
                var profileGrp = new CmdArgumentJson { Command = x.Key, LaunchProfile = x.Key, Items = new List<CmdArgumentJson>() };
                profileGrp.Items.Add(new CmdArgumentJson { Command = x.Value, Enabled = true });
                return profileGrp;
            });

            allArgs.AddRange(profileGrps);
        }

        private static Dictionary<Guid, ProjectArgumentsHandlers> supportedProjects = new Dictionary<Guid, ProjectArgumentsHandlers>()
        {
            // C#
            {ProjectKinds.CS, new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetMultiConfigArguments(project, arguments, "StartArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "StartArguments")
            } },
            // VB.NET
            {ProjectKinds.VB, new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetMultiConfigArguments(project, arguments, "StartArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "StartArguments")
            } },
            // C/C++
            {ProjectKinds.CPP, new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetVCProjEngineArguments(project, arguments),
                GetAllArguments = (project, allArgs) => GetVCProjEngineAllArguments(project, allArgs)
            } },
            // Python
            {ProjectKinds.Py, new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetSingleConfigArgument(project, arguments, "CommandLineArguments"),
                GetAllArguments = (project, allArgs) => GetSingleConfigAllArguments(project, allArgs, "CommandLineArguments")
            } },
            // Node.js
            {ProjectKinds.Node, new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetSingleConfigArgument(project, arguments, "ScriptArguments"),
                GetAllArguments = (project, allArgs) => GetSingleConfigAllArguments(project, allArgs, "ScriptArguments")
            } },
            // C# - Lagacy DotNetCore
            {ProjectKinds.CSCore, new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetCpsProjectArguments(project, arguments),
                GetAllArguments = (project, allArgs) => GetCpsProjectAllArguments(project, allArgs)
            } },
            // F#
            {ProjectKinds.FS, new ProjectArgumentsHandlers() {
                SetArguments = (project, arguments) => SetMultiConfigArguments(project, arguments, "StartArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "StartArguments")
            } },
        };

        public static bool IsSupportedProject(Microsoft.VisualStudio.Shell.Interop.IVsHierarchy project)
        {
            if (project == null)
                return false;

            // Issue #52:
            // Excludes a magic and strange SingleFileIntelisens pseudo project in %appdata%\Roaming\Microsoft\VisualStudio\<VS_Version>\SingleFileISense
            // Seems like that all of these _sfi_***.vcxproj files have the same magic guid
            // https://blogs.msdn.microsoft.com/vcblog/2015/04/29/single-file-intellisense-and-other-ide-improvements-in-vs2015/
            // https://support.sourcegear.com/viewtopic.php?f=5&t=22778
            if (project.GetGuid() == new Guid("a7a2a36c-3c53-4ccb-b52e-425623e2dda5"))
                return false;

            // Issue #113:
            // Shared projects are not supported as they are not runnable.
            // However, they share the same project kind GUID with other projects.
            if (project.IsSharedAssetsProject())
                return false;

            return supportedProjects.ContainsKey(project.GetKind());
        }

        public static void AddAllArguments(IVsHierarchy project, List<CmdArgumentJson> allArgs)
        {
            if (project.IsCpsProject())
            {
                Logger.Info($"Reading arguments on CPS project '{project.GetGuid()}' of type '{project.GetKind()}'.");
                GetCpsProjectAllArguments(project.GetProject(), allArgs);
            }
            else
            {
                ProjectArgumentsHandlers handler;
                if (supportedProjects.TryGetValue(project.GetKind(), out handler))
                {
                    handler.GetAllArguments(project.GetProject(), allArgs);
                }
            }
        }

        public static void SetArguments(IVsHierarchy project, string arguments)
        {
            if (project.IsCpsProject())
            {
                Logger.Info($"Setting arguments on CPS project of type '{project.GetKind()}'.");
                SetCpsProjectArguments(project.GetProject(), arguments);
            }
            else
            {
                ProjectArgumentsHandlers handler;
                if (supportedProjects.TryGetValue(project.GetKind(), out handler))
                {
                    handler.SetArguments(project.GetProject(), arguments);
                }
            }
        }
    }

    static class ProjectKinds
    {
        public static readonly Guid CS = Guid.Parse("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
        public static readonly Guid VB = Guid.Parse("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}");
        public static readonly Guid CPP = Guid.Parse("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}");
        public static readonly Guid Py = Guid.Parse("{888888a0-9f3d-457c-b088-3a5042f75d52}");
        public static readonly Guid Node = Guid.Parse("{9092aa53-fb77-4645-b42d-1ccca6bd08bd}");
        public static readonly Guid FS = Guid.Parse("{f2a71f9b-5d33-465a-a702-920d77279786}");

        /// <summary>
        /// Lagacy project type GUID for C# .Net core projects.
        /// In recent versions of VS this GUID is not used anymore.
        /// see: https://github.com/dotnet/project-system/issues/1821
        /// </summary>
        public static readonly Guid CSCore = Guid.Parse("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}");
        
    }
}
