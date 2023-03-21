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
    public static class ProjectConfigHelper
    {
        private class ProjectConfigHandlers
        {
            public delegate void SetConfigDelegate(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars);
            public delegate void GetAllArgumentsDelegate(EnvDTE.Project project, List<CmdArgumentJson> allArgs);
            public SetConfigDelegate SetConfig;
            public GetAllArgumentsDelegate GetAllArguments;
        }

        private static string GetEnvVarStringFromDict(IDictionary<string, string> envVars)
            => string.Join(Environment.NewLine, envVars.Select(x => $"{x.Key}={x.Value}"));

        private static IDictionary<string, string> GetEnvVarDictFromString(string envVars)
            => envVars
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split(new[] {'='}, 2))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0].Trim(), x => x[1].Trim());

        #region SingleConfig

        private static void SetSingleConfigArgument(EnvDTE.Project project, string arguments, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try { project.Properties.Item(propertyName).Value = arguments; }
            catch (Exception ex) { Logger.Error($"Failed to set single config arguments for project '{project.UniqueName}' with error '{ex}'"); }
        }

        // I don't know if this works for every single config project system, but for NodeJs and Python it works
        // see method Microsoft.VisualStudioTools.Project.ProjectNode.SetProjectProperty in:
        //  - https://github.com/microsoft/nodejstools (for NodeJS)
        //  - https://github.com/microsoft/PTVS (for Python)
        private static void SetSingleConfigEnvVars(EnvDTE.Project project, IDictionary<string, string> envVars, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var dynamicProject = (dynamic)project;
                var internalProject = dynamicProject.Project as object;

                var type = internalProject.GetType();

                var method = type.GetMethod("SetProjectProperty");
                method.Invoke(internalProject, new[] { propertyName, GetEnvVarStringFromDict(envVars) });
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set single config property '{propertyName}' for project '{project.UniqueName}' with error '{ex}'");
            }
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

        private static void GetSingleConfigAllEnvVars(EnvDTE.Project project, List<CmdArgumentJson> allArgs, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var dynamicProject = (dynamic)project;
                var internalProject = dynamicProject.Project as object;

                var type = internalProject.GetType();

                var method = type.GetMethod("GetProjectProperty");
                var envVars = method.Invoke(internalProject, new[] { propertyName });

                if (envVars is string envVarsString)
                {
                    foreach (var envVarPair in GetEnvVarDictFromString(envVarsString))
                    {
                        allArgs.Add(new CmdArgumentJson { Command = $"{envVarPair.Key}={envVarPair.Value}", Enabled = true });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set single config property '{propertyName}' for project '{project.UniqueName}' with error '{ex}'");
            }
        }

        #endregion SingleConfig

        #region MultiConfig

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
                        var configGrp = new CmdArgumentJson { Command = config.ConfigurationName, ProjectConfig = config.ConfigurationName, ProjectPlatform = config.PlatformName, Items = new List<CmdArgumentJson>() };
                        configGrp.Items.Add(new CmdArgumentJson { Command = cmdarg, Enabled = true });
                        allArgs.Add(configGrp);
                    }
                }
                catch (Exception ex) { Logger.Error($"Failed to get multi config arguments for project '{project.UniqueName}' with error '{ex}'"); }
            }
        }

        #endregion MultiConfig

        #region VCProjEngine (C/C++)

        private static readonly List<(string RuleName, string ArgsPropName, string EnvPropName)> VCPropInfo = new List<(string RuleName, string PropName, string EnvPropName)>
        {
            ("WindowsLocalDebugger", "LocalDebuggerCommandArguments", "LocalDebuggerEnvironment"),
            ("WindowsRemoteDebugger", "RemoteDebuggerCommandArguments", "RemoteDebuggerEnvironment"),
            ("LinuxWSLDebugger", "RemoteDebuggerCommandArguments", "RemoteDebuggerEnvironment"),
            ("GoogleAndroidDebugger", "LaunchFlags", null),
            ("GamingDesktopDebugger", "CommandLineArgs", null),
            ("OasisNXDebugger", "RemoteDebuggerCommandArguments", "RemoteDebuggerEnvironment"),
        };

        private static void SetVCProjEngineConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Use late binding to support VS2015 and VS2017
            dynamic vcPrj = (dynamic)project.Object; // is VCProject
            dynamic vcCfg = vcPrj?.ActiveConfiguration; // is VCConfiguration

            if (vcCfg == null)
            {
                Logger.Info("SetVCProjEngineArguments: VCProject?.ActiveConfiguration returned null");
                return;
            }

            var environmentString = GetEnvVarStringFromDict(envVars);

            // apply it first using the old way, in case the new way doesn't work for this type of projects (platforms other than Windows, for example)
            dynamic vcDbg = vcCfg.DebugSettings;  // is VCDebugSettings
            if (vcDbg != null)
            {
                vcDbg.CommandArguments = arguments;
                vcDbg.Environment = environmentString;
            }
            else
                Logger.Info("SetVCProjEngineArguments: VCProject?.ActiveConfiguration?.DebugSettings returned null");

            foreach (var vcPropInfo in VCPropInfo)
            {
                dynamic rule = vcCfg.Rules.Item(vcPropInfo.RuleName); // is IVCRulePropertyStorage
                if (rule != null)
                {
                    rule.SetPropertyValue(vcPropInfo.ArgsPropName, arguments);

                    if (vcPropInfo.EnvPropName != null)
                    {
                        rule.SetPropertyValue(vcPropInfo.EnvPropName, environmentString);
                    }
                }
                else
                    Logger.Info($"SetVCProjEngineArguments: ProjectConfig Rule '{vcPropInfo.RuleName}' returned null");
            }
        }

        private static void GetVCProjEngineConfig(EnvDTE.Project project, List<CmdArgumentJson> allArgs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic vcPrj = (dynamic)project.Object; // is VCProject
            dynamic configs = vcPrj?.Configurations;  // is IVCCollection

            if (configs == null)
            {
                Logger.Info("GetVCProjEngineConfig: VCProject.Configurations is null");
                return;
            }

            for (int index = 1; index <= configs.Count; index++)
            {
                dynamic cfg = configs.Item(index); // is VCConfiguration
                dynamic dbg = cfg.DebugSettings;  // is VCDebugSettings

                var items = new List<CmdArgumentJson>();

                if (!string.IsNullOrEmpty(dbg?.CommandArguments))
                    items.Add(dbg.CommandArguments);

                foreach (var vcPropInfo in VCPropInfo)
                {
                    dynamic rule = cfg.Rules.Item(vcPropInfo.RuleName); // is IVCRulePropertyStorage
                    if (rule != null)
                    {
                        var args = rule.GetUnevaluatedPropertyValue(vcPropInfo.ArgsPropName);
                        if (!string.IsNullOrEmpty(args))
                            items.Add(new CmdArgumentJson { Type = ViewModel.ArgumentType.CmdArg, Command = args, Enabled = true });

                        var envVars = rule.GetUnevaluatedPropertyValue(vcPropInfo.EnvPropName);
                        if (!string.IsNullOrEmpty(args))
                        {
                            foreach (var envVarPair in GetEnvVarDictFromString(envVars))
                            {
                                items.Add(new CmdArgumentJson {
                                    Type = ViewModel.ArgumentType.EnvVar,
                                    Command = $"{envVarPair.Key}={envVarPair.Value}",
                                    Enabled = true
                                });
                            }
                        }
                    }
                    else Logger.Info($"GetVCProjEngineAllArguments: ProjectConfig Rule '{vcPropInfo.RuleName}' returned null");
                }

                if (items.Count > 0)
                {
                    allArgs.Add(new CmdArgumentJson {
                        Command = cfg.ConfigurationName,
                        ProjectConfig = cfg.ConfigurationName,
                        ProjectPlatform = cfg.PlatformName,
                        Items = items
                    });
                }
            }
        }

        #endregion VCProjEngine (C/C++)

        #region VFProjEngine (Fortran)

        private static string VFFormatConfigName(EnvDTE.Configuration vcCfg)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return $"{vcCfg.ConfigurationName}|{vcCfg.PlatformName}";
        }

        // We have to get the active configuration form ConfigurationManager.ActiveConfiguration
        // (because `Project.Object.ActiveConfiguration` trows an `RuntimeBinderException`)
        // but there the `Properties` property is `null`. Therefore we have to go a different
        // route to set the arguments. We generate a name from the `Configuration` and use it
        // to optain the right configurations object from `Project.Object.Configurations`
        // this object is simmilar to the VCProjEngine configuration and has `DebugSettings`
        // which contain the CommandArguments which we can use to set the args.
        private static void SetVFProjEngineConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vcCfg = project?.ConfigurationManager?.ActiveConfiguration; // is VCConfiguration

            if (vcCfg == null)
            {
                Logger.Info("SetVFProjEngineArguments: VCProject?.ConfigurationManager?.ActiveConfiguration returned null");
                return;
            }

            // Use late binding to support VS2015 and VS2017
            dynamic activeFortranConfig = ((dynamic)project.Object).Configurations.Item(VFFormatConfigName(vcCfg));

            dynamic vfDbg = activeFortranConfig.DebugSettings;  // is VCDebugSettings
            if (vfDbg != null)
            {
                vfDbg.CommandArguments = arguments;
                vfDbg.Environment = GetEnvVarStringFromDict(envVars);
            }
            else
                Logger.Info("SetVCProjEngineArguments: VCProject?.ActiveConfiguration?.DebugSettings returned null");
        }

        // Here we go the same way as in SetVFProjEngineArguments because we need the `ConfigurationName`
        // which isn't included in the objects obtained form `Project.Object.Configurations`. It's a bit
        // missleading because a property called `ConfigurationName` exists there but when called throws
        // an NotImplementedException.
        private static void GetVFProjEngineConfig(EnvDTE.Project project, List<CmdArgumentJson> allArgs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic vcPrj = (dynamic)project.Object; // is VCProject
            dynamic configs = vcPrj?.Configurations;  // is IVCCollection
            var cfgManager = project?.ConfigurationManager;

            if (configs == null)
            {
                Logger.Info("GetVFProjEngineConfig: VCProject.Configurations is null");
                return;
            }

            for (int index = 1; index <= cfgManager.Count; index++)
            {
                var vcCfg = cfgManager.Item(index);
                dynamic vfCfg = configs.Item(VFFormatConfigName(vcCfg)); // is VCConfiguration
                dynamic dbg = vfCfg.DebugSettings;  // is VCDebugSettings

                var items = new List<CmdArgumentJson>();

                if (!string.IsNullOrEmpty(dbg?.CommandArguments))
                {
                    items.Add(new CmdArgumentJson { Command = dbg.CommandArguments, Enabled = true });
                }

                if (!string.IsNullOrEmpty(dbg?.Environment))
                {
                    foreach (var envVarPair in GetEnvVarDictFromString(dbg.Environment))
                    {
                        items.Add(new CmdArgumentJson
                        {
                            Type = ViewModel.ArgumentType.EnvVar,
                            Command = $"{envVarPair.Key}={envVarPair.Value}",
                            Enabled = true
                        });
                    }
                }

                if (items.Count > 0)
                {
                    allArgs.Add(new CmdArgumentJson {
                        Command = vcCfg.ConfigurationName,
                        ProjectConfig = vcCfg.ConfigurationName,
                        ProjectPlatform = vcCfg.PlatformName,
                        Items = items
                    });
                }
            }
        }

        #endregion VFProjEngine (Fortran)

        #region Common Project System (CPS)

        private static void SetCpsProjectConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars)
        {
            // Should only be called in VS 2017 or higher
            // .Net Core 2 is not supported by VS 2015, so this should not cause problems
            CpsProjectSupport.SetCpsProjectConfig(project, arguments, envVars);
        }

        private static void GetCpsProjectConfig(EnvDTE.Project project, List<CmdArgumentJson> allArgs)
        {
            // Should only be called in VS 2017 or higher
            // see SetCpsProjectArguments
            allArgs.AddRange(CpsProjectSupport.GetCpsProjectAllArguments(project));
        }

        #endregion Common Project System (CPS)

        private static Dictionary<Guid, ProjectConfigHandlers> supportedProjects = new Dictionary<Guid, ProjectConfigHandlers>()
        {
            // C#
            {ProjectKinds.CS, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, _) => SetMultiConfigArguments(project, arguments, "StartArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "StartArguments")
            } },
            // VB.NET
            {ProjectKinds.VB, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, _) => SetMultiConfigArguments(project, arguments, "StartArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "StartArguments")
            } },
            // C/C++
            {ProjectKinds.CPP, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, envVars) => SetVCProjEngineConfig(project, arguments, envVars),
                GetAllArguments = (project, allArgs) => GetVCProjEngineConfig(project, allArgs)
            } },
            // Python
            {ProjectKinds.Py, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, envVars) => {
                    SetSingleConfigArgument(project, arguments, "CommandLineArguments");
                    SetSingleConfigEnvVars(project, envVars, "Environment");
                },
                GetAllArguments = (project, allArgs) => {
                    GetSingleConfigAllArguments(project, allArgs, "CommandLineArguments");
                    GetSingleConfigAllEnvVars(project, allArgs, "Environment");
                }
            } },
            // Node.js
            {ProjectKinds.Node, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, envVars) => {
                    SetSingleConfigArgument(project, arguments, "ScriptArguments");
                    SetSingleConfigEnvVars(project, envVars, "Environment");
                },
                GetAllArguments = (project, allArgs) => {
                    GetSingleConfigAllArguments(project, allArgs, "ScriptArguments");
                    GetSingleConfigAllEnvVars(project, allArgs, "Environment");
                }
            } },
            // C# - Lagacy DotNetCore
            {ProjectKinds.CSCore, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, envVars) => SetCpsProjectConfig(project, arguments, envVars),
                GetAllArguments = (project, allArgs) => GetCpsProjectConfig(project, allArgs)
            } },
            // F#
            {ProjectKinds.FS, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, _) => SetMultiConfigArguments(project, arguments, "StartArguments"),
                GetAllArguments = (project, allArgs) => GetMultiConfigAllArguments(project, allArgs, "StartArguments")
            } },
            // Fortran
            {ProjectKinds.Fortran, new ProjectConfigHandlers() {
                SetConfig = (project, arguments, envVars) => SetVFProjEngineConfig(project, arguments, envVars),
                GetAllArguments = (project, allArgs) => GetVFProjEngineConfig(project, allArgs)
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
                GetCpsProjectConfig(project.GetProject(), allArgs);
            }
            else
            {
                ProjectConfigHandlers handler;
                if (supportedProjects.TryGetValue(project.GetKind(), out handler))
                {
                    handler.GetAllArguments(project.GetProject(), allArgs);
                }
            }
        }

        public static void SetConfig(IVsHierarchy project, string arguments, IDictionary<string, string> envVars)
        {
            if (project.IsCpsProject())
            {
                Logger.Info($"Setting arguments on CPS project of type '{project.GetKind()}'.");
                SetCpsProjectConfig(project.GetProject(), arguments, envVars);
            }
            else
            {
                ProjectConfigHandlers handler;
                if (supportedProjects.TryGetValue(project.GetKind(), out handler))
                {
                    handler.SetConfig(project.GetProject(), arguments, envVars);
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
        public static readonly Guid Fortran = Guid.Parse("{7c1dcf51-7319-4793-8f63-17f648d2e313}");

        /// <summary>
        /// Lagacy project type GUID for C# .Net core projects.
        /// In recent versions of VS this GUID is not used anymore.
        /// see: https://github.com/dotnet/project-system/issues/1821
        /// </summary>
        public static readonly Guid CSCore = Guid.Parse("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}");
        
    }
}
