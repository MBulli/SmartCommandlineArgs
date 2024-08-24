using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.Helper;
using SmartCmdArgs.DataSerialization;
using SmartCmdArgs.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartCmdArgs.Services
{
    public interface IProjectConfigService
    {
        bool IsSupportedProject(IVsHierarchyWrapper project);

        List<CmdItemJson> GetItemsFromProjectConfig(IVsHierarchyWrapper project);

        void UpdateProjectConfig(IVsHierarchyWrapper project);
    }

    public class ProjectConfigService : IProjectConfigService
    {
        private readonly Lazy<ILifeCycleService> lifeCycleService;
        private readonly IOptionsSettingsService optionsSettings;
        private readonly IItemAggregationService itemAggregationService;
        private readonly ICpsProjectConfigService cpsProjectConfigService;

        private readonly Dictionary<Guid, ProjectConfigHandlers> configHandlerForSupportedProjects;
        private readonly ProjectConfigHandlers cpsProjectConfigHandlers;

        public ProjectConfigService(
            Lazy<ILifeCycleService> lifeCycleService,
            IOptionsSettingsService optionsSettings,
            IItemAggregationService itemAggregationService,
            ICpsProjectConfigService cpsProjectConfigService)
        {
            this.lifeCycleService = lifeCycleService;
            this.optionsSettings = optionsSettings;
            this.itemAggregationService = itemAggregationService;
            this.cpsProjectConfigService = cpsProjectConfigService;

            configHandlerForSupportedProjects = InitConfigHandlerForSupportedProjects();
            cpsProjectConfigHandlers = new ProjectConfigHandlers
            {
                GetItemsFromConfig = cpsProjectConfigService.GetItemsFromConfig,
                SetConfig = cpsProjectConfigService.SetConfig,
            };
        }

        private class ProjectConfigHandlers
        {
            public delegate void SetConfigDelegate(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars, string workDir, string launchApp);
            public delegate void GetAllArgumentsDelegate(EnvDTE.Project project, List<CmdItemJson> allArgs, bool includeArgs, bool includeEnvVars, bool includeWorkDir, bool includeLaunchApp);
            public SetConfigDelegate SetConfig;
            public GetAllArgumentsDelegate GetItemsFromConfig;
        }
        private Dictionary<Guid, ProjectConfigHandlers> InitConfigHandlerForSupportedProjects()
        {
            return new Dictionary<Guid, ProjectConfigHandlers>()
            {
                // C#
                {ProjectKinds.CS, new ProjectConfigHandlers() {
                    SetConfig = (project, arguments, envVars, workDir, launchApp) => {
                        SetMultiConfigProperty(project, arguments, "StartArguments");
                        SetMultiConfigProperty(project, workDir, "StartWorkingDirectory");
                        SetMultiConfigProperty(project, launchApp, "StartProgram");
                    },
                    GetItemsFromConfig = GetItemsFromMultiConfig("StartArguments", "StartWorkingDirectory", "StartProgram")
                } },
                // C# UWP
                {ProjectKinds.CS_UWP, new ProjectConfigHandlers() {
                    SetConfig = (project, arguments, envVars, workDir, launchApp) => SetMultiConfigProperty(project, arguments, "UAPDebug.CommandLineArguments"),
                    GetItemsFromConfig = GetItemsFromMultiConfig("UAPDebug.CommandLineArguments")
                } },

                // VB.NET
                {ProjectKinds.VB, new ProjectConfigHandlers() {
                    SetConfig = (project, arguments, _, workDir, launchApp) => {
                        SetMultiConfigProperty(project, arguments, "StartArguments");
                        SetMultiConfigProperty(project, workDir, "StartWorkingDirectory");
                        SetMultiConfigProperty(project, launchApp, "StartProgram");
                    },
                    GetItemsFromConfig = GetItemsFromMultiConfig("StartArguments", "StartWorkingDirectory", "StartProgram")
                } },
                // C/C++
                {ProjectKinds.CPP, new ProjectConfigHandlers() {
                    SetConfig = SetVCProjEngineConfig,
                    GetItemsFromConfig = GetItemsFromVCProjEngineConfig
                } },
                // Python
                {ProjectKinds.Py, new ProjectConfigHandlers() {
                    SetConfig = (project, arguments, envVars, workDir, _) => {
                        SetSingleConfigProperty(project, arguments, "CommandLineArguments");
                        SetSingleConfigEnvVars(project, envVars, "Environment");
                        SetSingleConfigProperty(project, workDir, "WorkingDirectory");
                    },
                    GetItemsFromConfig = GetItemsFromSingleConfig("CommandLineArguments", "Environment", "WorkingDirectory", null),
                } },
                // Node.js
                {ProjectKinds.Node, new ProjectConfigHandlers() {
                    SetConfig = (project, arguments, envVars, workDir, _) => {
                        SetSingleConfigProperty(project, arguments, "ScriptArguments");
                        SetSingleConfigEnvVars(project, envVars, "Environment");
                        SetSingleConfigProperty(project, workDir, "WorkingDirectory");
                    },
                    GetItemsFromConfig = GetItemsFromSingleConfig("ScriptArguments", "Environment", "WorkingDirectory", null),
                } },
                // C# - Lagacy DotNetCore
                {ProjectKinds.CSCore, new ProjectConfigHandlers() {
                    SetConfig = cpsProjectConfigService.SetConfig,
                    GetItemsFromConfig = cpsProjectConfigService.GetItemsFromConfig,
                } },
                // F#
                {ProjectKinds.FS, new ProjectConfigHandlers() {
                    SetConfig = (project, arguments, _, workDir, launchApp) => {
                        SetMultiConfigProperty(project, arguments, "StartArguments");
                        SetMultiConfigProperty(project, workDir, "StartWorkingDirectory");
                        SetMultiConfigProperty(project, launchApp, "StartProgram");
                    },
                    GetItemsFromConfig = GetItemsFromMultiConfig("StartArguments", "StartWorkingDirectory", "StartProgram")
                } },
                // Fortran
                {ProjectKinds.Fortran, new ProjectConfigHandlers() {
                    SetConfig = SetVFProjEngineConfig,
                    GetItemsFromConfig = GetItemsFromVFProjEngineConfig
                } },
            };
        }
        

        private static string GetEnvVarStringFromDict(IDictionary<string, string> envVars)
            => string.Join(Environment.NewLine, envVars.Select(x => $"{x.Key}={x.Value}"));

        private static IDictionary<string, string> GetEnvVarDictFromString(string envVars)
            => envVars
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split(new[] { '=' }, 2))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0].Trim(), x => x[1].Trim());

        #region SingleConfig

        private static void SetSingleConfigProperty(EnvDTE.Project project, string value, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (value == null)
                return;

            try
            {
                project.Properties.Item(propertyName).Value = value;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set single config property '{propertyName}' for project '{project.UniqueName}' with error '{ex}'");
            }
        }

        // I don't know if this works for every single config project system, but for NodeJs and Python it works
        // see method Microsoft.VisualStudioTools.Project.ProjectNode.SetProjectProperty in:
        //  - https://github.com/microsoft/nodejstools (for NodeJS)
        //  - https://github.com/microsoft/PTVS (for Python)
        private static void SetSingleConfigEnvVars(EnvDTE.Project project, IDictionary<string, string> envVars, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (envVars == null)
                return;

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

        private static bool TryGetSingleConfigProperty(EnvDTE.Project project, string propertyName, out string result)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                result = project.Properties.Item(propertyName).Value as string;
                return result != null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get single config property '{propertyName}' for project '{project.UniqueName}' with error '{ex}'");
                result = null;
                return false;
            }
        }

        private static bool TryGetSingleConfigInternalProperty(EnvDTE.Project project, string propertyName, out string result)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var dynamicProject = (dynamic)project;
                var internalProject = dynamicProject.Project as object;

                var type = internalProject.GetType();

                var method = type.GetMethod("GetProjectProperty");
                result = method.Invoke(internalProject, new[] { propertyName }) as string;

                return result != null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get internal single config property '{propertyName}' for project '{project.UniqueName}' with error '{ex}'");
                result = null;
                return false;
            }
        }

        private static ProjectConfigHandlers.GetAllArgumentsDelegate GetItemsFromSingleConfig(string argsPropName, string envVarPropName, string workDirPropName, string launchAppPropName)
        {
            return (EnvDTE.Project project, List<CmdItemJson> allArgs, bool includeArgs, bool includeEnvVars, bool includeWorkDir, bool includeLaunchApp) =>
            {
                if (includeArgs && argsPropName != null && TryGetSingleConfigProperty(project, argsPropName, out string args))
                {
                    allArgs.Add(new CmdItemJson
                    {
                        Type = ViewModel.CmdParamType.CmdArg,
                        Command = args,
                        Enabled = true,
                    });
                }

                if (includeEnvVars && envVarPropName != null && TryGetSingleConfigInternalProperty(project, envVarPropName, out string envVarsStr))
                {
                    foreach (var envVarPair in GetEnvVarDictFromString(envVarsStr))
                    {
                        allArgs.Add(new CmdItemJson
                        {
                            Type = ViewModel.CmdParamType.EnvVar,
                            Command = $"{envVarPair.Key}={envVarPair.Value}",
                            Enabled = true,
                        });
                    }
                }

                if (includeWorkDir && workDirPropName != null && TryGetSingleConfigProperty(project, workDirPropName, out string workDir))
                {
                    allArgs.Add(new CmdItemJson
                    {
                        Type = ViewModel.CmdParamType.WorkDir,
                        Command = workDir,
                        Enabled = true,
                    });
                }

                if (includeLaunchApp && launchAppPropName != null && TryGetSingleConfigProperty(project, launchAppPropName, out string launchApp))
                {
                    allArgs.Add(new CmdItemJson
                    {
                        Type = ViewModel.CmdParamType.LaunchApp,
                        Command = launchApp,
                        Enabled = true,
                    });
                }
            };
        }

        #endregion SingleConfig

        #region MultiConfig

        private static void SetMultiConfigProperty(EnvDTE.Project project, string value, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (value == null)
                return;

            // Set the arguments only on the active configuration
            EnvDTE.Properties properties = project.ConfigurationManager?.ActiveConfiguration?.Properties;
            try { properties.Item(propertyName).Value = value; }
            catch (Exception ex) { Logger.Error($"Failed to set multi config arguments for project '{project.UniqueName}' with error '{ex}'"); }
        }

        private static ProjectConfigHandlers.GetAllArgumentsDelegate GetItemsFromMultiConfig(string argsPropName, string workDirPropName = null, string launchAppPropName = null)
        {
            return (EnvDTE.Project project, List<CmdItemJson> allArgs, bool includeArgs, bool includeEnvVars, bool includeWorkDir, bool includeLaunchApp) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Read properties for all configurations (e.g. Debug/Release)
                foreach (EnvDTE.Configuration config in project.ConfigurationManager)
                {
                    try
                    {
                        var items = new List<CmdItemJson>();

                        if (includeArgs)
                        {
                            string args = config.Properties.Item(argsPropName)?.Value as string;
                            if (!string.IsNullOrEmpty(args))
                            {
                                items.Add(new CmdItemJson
                                {
                                    Type = ViewModel.CmdParamType.CmdArg,
                                    Command = args,
                                    Enabled = true,
                                });
                            }
                        }

                        if (includeWorkDir && workDirPropName != null)
                        {
                            string workDir = config.Properties.Item(workDirPropName)?.Value as string;
                            if (!string.IsNullOrEmpty(workDir))
                            {
                                items.Add(new CmdItemJson
                                {
                                    Type = ViewModel.CmdParamType.WorkDir,
                                    Command = workDir,
                                    Enabled = true,
                                });
                            }
                        }

                        if (includeLaunchApp && launchAppPropName != null)
                        {
                            string launcApp = config.Properties.Item(launchAppPropName)?.Value as string;
                            if (!string.IsNullOrEmpty(launcApp))
                            {
                                items.Add(new CmdItemJson
                                {
                                    Type = ViewModel.CmdParamType.LaunchApp,
                                    Command = launcApp,
                                    Enabled = true,
                                });
                            }
                        }

                        if (items.Count > 0)
                        {
                            allArgs.Add(new CmdItemJson
                            {
                                Command = config.ConfigurationName,
                                ProjectConfig = config.ConfigurationName,
                                ProjectPlatform = config.PlatformName,
                                Items = items,
                            });
                        }
                    }
                    catch (Exception ex) { Logger.Error($"Failed to get multi config arguments for project '{project.UniqueName}' with error '{ex}'"); }
                }
            };
        }

        #endregion MultiConfig

        #region VCProjEngine (C/C++)

        private static readonly List<(string RuleName, string ArgsPropName, string EnvPropName, string WorkDirPropName, string LaunchAppPropName)> VCPropInfo = new List<(string RuleName, string PropName, string EnvPropName, string WorkDirPropName, string LaunchAppPropName)>
            {
                ("WindowsLocalDebugger", "LocalDebuggerCommandArguments", "LocalDebuggerEnvironment", "LocalDebuggerWorkingDirectory", "LocalDebuggerCommand"),
                ("WindowsRemoteDebugger", "RemoteDebuggerCommandArguments", "RemoteDebuggerEnvironment", "RemoteDebuggerWorkingDirectory", "RemoteDebuggerCommand"),
                ("LinuxWSLDebugger", "RemoteDebuggerCommandArguments", "RemoteDebuggerEnvironment", "RemoteDebuggerWorkingDirectory", "RemoteDebuggerCommand"),
                ("GoogleAndroidDebugger", "LaunchFlags", null, null, null),
                ("GamingDesktopDebugger", "CommandLineArgs", null, null, null),
                ("OasisNXDebugger", "RemoteDebuggerCommandArguments", "RemoteDebuggerEnvironment", "RemoteDebuggerWorkingDirectory", "RemoteDebuggerCommand"),
                ("LinuxDebugger", "RemoteDebuggerCommandArguments", null, "RemoteDebuggerWorkingDirectory", null),
                ("AppHostLocalDebugger", "CommandLineArgs", null, null, null),
            };

        private static void SetVCProjEngineConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars, string workDir, string launchApp)
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

            var environmentString = envVars == null ? null : GetEnvVarStringFromDict(envVars);

            // apply it first using the old way, in case the new way doesn't work for this type of projects (platforms other than Windows, for example)
            // TODO: eliminate this way of setting stuff to avoid clutter in the *.user file
            //       with this approach there are always entries for LocalDebuggerCommandArguments and LocalDebuggerEnvironment
            //       which is the same as the rule WindowsLocalDebugger
            dynamic vcDbg = vcCfg.DebugSettings;  // is VCDebugSettings
            if (vcDbg != null)
            {
                if (arguments != null)
                    vcDbg.CommandArguments = arguments;

                if (environmentString != null)
                    vcDbg.Environment = environmentString;

                if (workDir != null)
                    vcDbg.WorkingDirectory = workDir;

                if (launchApp != null)
                    vcDbg.Command = launchApp;
            }
            else
                Logger.Info("SetVCProjEngineArguments: VCProject?.ActiveConfiguration?.DebugSettings returned null");

            foreach (var vcPropInfo in VCPropInfo)
            {
                dynamic rule = vcCfg.Rules.Item(vcPropInfo.RuleName); // is IVCRulePropertyStorage
                if (rule != null)
                {
                    if (arguments != null)
                        rule.SetPropertyValue(vcPropInfo.ArgsPropName, arguments);

                    if (vcPropInfo.EnvPropName != null && environmentString != null)
                        rule.SetPropertyValue(vcPropInfo.EnvPropName, environmentString);

                    if (vcPropInfo.WorkDirPropName != null && workDir != null)
                        rule.SetPropertyValue(vcPropInfo.WorkDirPropName, workDir);

                    if (vcPropInfo.LaunchAppPropName != null && launchApp != null)
                        rule.SetPropertyValue(vcPropInfo.LaunchAppPropName, launchApp);
                }
                else
                    Logger.Info($"SetVCProjEngineArguments: ProjectConfig Rule '{vcPropInfo.RuleName}' returned null");
            }
        }

        private static void GetItemsFromVCProjEngineConfig(EnvDTE.Project project, List<CmdItemJson> allArgs, bool includeArgs, bool includeEnvVars, bool includeWorkDir, bool includeLaunchApp)
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

                var items = new List<CmdItemJson>();

                var foundActiveFlavour = false;

                string activeDebuggerFlavour = cfg.Rules.Item("DebuggerGeneralProperties")?.GetUnevaluatedPropertyValue("DebuggerFlavor");
                foreach (var vcPropInfo in VCPropInfo)
                {
                    dynamic rule = cfg.Rules.Item(vcPropInfo.RuleName); // is IVCRulePropertyStorage
                    if (rule != null)
                    {
                        var isActiveRule = activeDebuggerFlavour == vcPropInfo.RuleName;
                        foundActiveFlavour |= isActiveRule;

                        var flavourItems = new List<CmdItemJson>();

                        if (includeArgs)
                        {
                            var args = rule.GetUnevaluatedPropertyValue(vcPropInfo.ArgsPropName);
                            if (!string.IsNullOrEmpty(args))
                            {
                                flavourItems.Add(new CmdItemJson
                                {
                                    Type = ViewModel.CmdParamType.CmdArg,
                                    Command = args,
                                    Enabled = isActiveRule,
                                });
                            }
                        }

                        if (includeEnvVars && vcPropInfo.EnvPropName != null)
                        {
                            var envVars = rule.GetUnevaluatedPropertyValue(vcPropInfo.EnvPropName);
                            if (!string.IsNullOrEmpty(envVars))
                            {
                                foreach (var envVarPair in GetEnvVarDictFromString(envVars))
                                {
                                    flavourItems.Add(new CmdItemJson
                                    {
                                        Type = ViewModel.CmdParamType.EnvVar,
                                        Command = $"{envVarPair.Key}={envVarPair.Value}",
                                        Enabled = isActiveRule,
                                    });
                                }
                            }
                        }

                        if (includeWorkDir && vcPropInfo.WorkDirPropName != null)
                        {
                            var workDir = rule.GetUnevaluatedPropertyValue(vcPropInfo.WorkDirPropName);
                            if (!string.IsNullOrEmpty(workDir))
                            {
                                flavourItems.Add(new CmdItemJson
                                {
                                    Type = ViewModel.CmdParamType.WorkDir,
                                    Command = workDir,
                                    Enabled = isActiveRule,
                                });
                            }
                        }

                        if (includeLaunchApp && vcPropInfo.LaunchAppPropName != null)
                        {
                            var launchApp = rule.GetUnevaluatedPropertyValue(vcPropInfo.LaunchAppPropName);
                            if (!string.IsNullOrEmpty(launchApp))
                            {
                                flavourItems.Add(new CmdItemJson
                                {
                                    Type = ViewModel.CmdParamType.LaunchApp,
                                    Command = launchApp,
                                    Enabled = isActiveRule,
                                });
                            }
                        }

                        if (flavourItems.Count > 0)
                        {
                            items.Add(new CmdItemJson { Command = vcPropInfo.RuleName, Items = flavourItems });
                        }
                    }
                    else Logger.Info($"GetVCProjEngineAllArguments: ProjectConfig Rule '{vcPropInfo.RuleName}' returned null");
                }

                if (!foundActiveFlavour)
                {
                    if (includeLaunchApp && !string.IsNullOrEmpty(dbg?.Command))
                    {
                        items.Insert(0, new CmdItemJson { Type = ViewModel.CmdParamType.LaunchApp, Command = dbg?.Command, Enabled = true });
                    }

                    if (includeWorkDir && !string.IsNullOrEmpty(dbg?.WorkingDirectory))
                    {
                        items.Insert(0, new CmdItemJson { Type = ViewModel.CmdParamType.WorkDir, Command = dbg?.WorkingDirectory, Enabled = true });
                    }

                    if (includeEnvVars && !string.IsNullOrEmpty(dbg?.Environment))
                    {
                        items.Insert(0, new CmdItemJson { Type = ViewModel.CmdParamType.EnvVar, Command = dbg?.Environment, Enabled = true });
                    }

                    if (includeArgs && !string.IsNullOrEmpty(dbg?.CommandArguments))
                    {
                        items.Insert(0, new CmdItemJson { Type = ViewModel.CmdParamType.CmdArg, Command = dbg?.CommandArguments, Enabled = true });
                    }
                }

                if (items.Count > 0)
                {
                    allArgs.Add(new CmdItemJson
                    {
                        Command = cfg.Name,
                        ProjectConfig = cfg.ConfigurationName,
                        ProjectPlatform = cfg.Platform.Name,
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
        private static void SetVFProjEngineConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars, string workDir, string launchApp)
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
                if (arguments != null)
                    vfDbg.CommandArguments = arguments;

                if (envVars != null)
                    vfDbg.Environment = GetEnvVarStringFromDict(envVars);

                if (workDir != null)
                    vfDbg.WorkingDirectory = workDir;

                if (launchApp != null)
                    vfDbg.Command = launchApp;
            }
            else
                Logger.Info("SetVCProjEngineArguments: VCProject?.ActiveConfiguration?.DebugSettings returned null");
        }

        // Here we go the same way as in SetVFProjEngineArguments because we need the `ConfigurationName`
        // which isn't included in the objects obtained form `Project.Object.Configurations`. It's a bit
        // missleading because a property called `ConfigurationName` exists there but when called throws
        // an NotImplementedException.
        private static void GetItemsFromVFProjEngineConfig(EnvDTE.Project project, List<CmdItemJson> allArgs, bool includeArgs, bool includeEnvVars, bool includeWorkDir, bool includeLaunchApp)
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

                var items = new List<CmdItemJson>();

                if (includeArgs && !string.IsNullOrEmpty(dbg?.CommandArguments))
                {
                    items.Add(new CmdItemJson
                    {
                        Type = ViewModel.CmdParamType.CmdArg,
                        Command = dbg.CommandArguments,
                        Enabled = true
                    });
                }

                if (includeEnvVars && !string.IsNullOrEmpty(dbg?.Environment))
                {
                    foreach (var envVarPair in GetEnvVarDictFromString(dbg.Environment))
                    {
                        items.Add(new CmdItemJson
                        {
                            Type = ViewModel.CmdParamType.EnvVar,
                            Command = $"{envVarPair.Key}={envVarPair.Value}",
                            Enabled = true
                        });
                    }
                }

                if (includeWorkDir && !string.IsNullOrEmpty(dbg?.WorkingDirectory))
                {
                    items.Add(new CmdItemJson
                    {
                        Type = ViewModel.CmdParamType.WorkDir,
                        Command = dbg.WorkingDirectory,
                        Enabled = true
                    });
                }

                if (includeLaunchApp && !string.IsNullOrEmpty(dbg?.Command))
                {
                    items.Add(new CmdItemJson
                    {
                        Type = ViewModel.CmdParamType.LaunchApp,
                        Command = dbg.Command,
                        Enabled = true
                    });
                }

                if (items.Count > 0)
                {
                    allArgs.Add(new CmdItemJson
                    {
                        Command = vcCfg.ConfigurationName,
                        ProjectConfig = vcCfg.ConfigurationName,
                        ProjectPlatform = vcCfg.PlatformName,
                        Items = items
                    });
                }
            }
        }

        #endregion VFProjEngine (Fortran)

        public bool IsSupportedProject(IVsHierarchyWrapper project)
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

            return configHandlerForSupportedProjects.ContainsKey(project.GetKind());
        }

        private bool TryGetProjectConfigHandlers(IVsHierarchyWrapper project, out ProjectConfigHandlers handler)
        {
            if (project.SupportsLaunchProfiles())
            {
                handler = cpsProjectConfigHandlers;
                return true;
            }

            var projectKind = project.GetKind();

            if (projectKind == ProjectKinds.CS)
            {
                foreach (var kind in project.GetAllTypeGuidsFromFile())
                {
                    if (kind != projectKind && configHandlerForSupportedProjects.TryGetValue(kind, out handler))
                    {
                        return true;
                    }
                }
            }

            return configHandlerForSupportedProjects.TryGetValue(projectKind, out handler);
        }

        public List<CmdItemJson> GetItemsFromProjectConfig(IVsHierarchyWrapper project)
        {
            var result = new List<CmdItemJson>();

            if (TryGetProjectConfigHandlers(project, out ProjectConfigHandlers handler))
            {
                handler.GetItemsFromConfig(
                    project.GetProject(),
                    result,
                    includeArgs: optionsSettings.ManageCommandLineArgs,
                    includeEnvVars: optionsSettings.ManageEnvironmentVars,
                    includeWorkDir: optionsSettings.ManageWorkingDirectories,
                    includeLaunchApp: optionsSettings.ManageLaunchApplication);
            }

            return result;
        }

        public void UpdateProjectConfig(IVsHierarchyWrapper project)
        {
            if (!lifeCycleService.Value.IsEnabled || project == null)
                return;

            var commandLineArgs = optionsSettings.ManageCommandLineArgs ? itemAggregationService.CreateCommandLineArgsForProject(project) : null;
            var envVars = optionsSettings.ManageEnvironmentVars ? itemAggregationService.GetEnvVarsForProject(project) : null;
            var workDir = optionsSettings.ManageWorkingDirectories ? itemAggregationService.GetWorkDirForProject(project) : null;
            var launchApp = optionsSettings.ManageLaunchApplication ? itemAggregationService.GetLaunchAppForProject(project) : null;

            if (commandLineArgs is null && envVars is null && workDir is null && launchApp is null)
                return;

            if (TryGetProjectConfigHandlers(project, out ProjectConfigHandlers handler))
            {
                handler.SetConfig(project.GetProject(), commandLineArgs, envVars, workDir, launchApp);
            }

            Logger.Info($"Updated Configuration for Project: {project.GetName()}");
        }
    }

    static class ProjectKinds
    {
        // main kinds
        public static readonly Guid CS = Guid.Parse("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
        public static readonly Guid VB = Guid.Parse("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}");
        public static readonly Guid CPP = Guid.Parse("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}");
        public static readonly Guid Py = Guid.Parse("{888888a0-9f3d-457c-b088-3a5042f75d52}");
        public static readonly Guid Node = Guid.Parse("{9092aa53-fb77-4645-b42d-1ccca6bd08bd}");
        public static readonly Guid FS = Guid.Parse("{f2a71f9b-5d33-465a-a702-920d77279786}");
        public static readonly Guid Fortran = Guid.Parse("{7c1dcf51-7319-4793-8f63-17f648d2e313}");

        // sub kinds
        public static readonly Guid CS_UWP = Guid.Parse("{A5A43C5B-DE2A-4C0C-9213-0A381AF9435A}");

        /// <summary>
        /// Lagacy project type GUID for C# .Net core projects.
        /// In recent versions of VS this GUID is not used anymore.
        /// see: https://github.com/dotnet/project-system/issues/1821
        /// </summary>
        public static readonly Guid CSCore = Guid.Parse("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}");

    }
}
