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
#if DYNAMIC_VSProjectManaged
using System.Reflection;
using System.Reflection.Emit;
using Expression = System.Linq.Expressions.Expression;
#endif

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
        void SetConfig(Project project, string arguments, IDictionary<string, string> envVars, string workDir, string launchApp, UpdateProjectConfigReason reason);
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

        public void SetConfig(EnvDTE.Project project, string arguments, IDictionary<string, string> envVars, string workDir, string launchApp, UpdateProjectConfigReason reason)
        {
            IUnconfiguredProjectServices unconfiguredProjectServices;
            IProjectServices projectServices;

            if (TryGetProjectServices(project, out unconfiguredProjectServices, out projectServices))
            {
                var launchSettingsProvider = unconfiguredProjectServices.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
                ILaunchProfile baseLaunchProfile = null;
                var applyProfileFix=true;
                if (optionsSettingsService.UseCpsVirtualProfile)
                {
                    baseLaunchProfile = launchSettingsProvider.CurrentSnapshot.Profiles.FirstOrDefault(x => x.Name == VirtualProfileName);
                }

                if (baseLaunchProfile == null)
                {
                    baseLaunchProfile = launchSettingsProvider?.ActiveProfile;
                }
                else
                    applyProfileFix = false; //we already existed

                var dbgsnapshot = launchSettingsProvider.CurrentSnapshot;
                var dbgactiveProfile = launchSettingsProvider.ActiveProfile;
                //OurLogger.Info(LogCat.Other, $"dbgsnapshot {dbgsnapshot?.ActiveProfile?.Name}, dbgactiveProfile {dbgactiveProfile?.Name}, baseLaunchProfile: {baseLaunchProfile?.Name} forceOurProfileActive: {applyProfileFix}");
                if (baseLaunchProfile == null)
                    return;

                var writableLaunchProfile = WritableLaunchProfile.GetWritableLaunchProfile(baseLaunchProfile);

                if (arguments != null)
                    writableLaunchProfile.CommandLineArgs = arguments;
                else
                    applyProfileFix = false;//incase not used on a solution
                
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
                var activeProfileBehavior = optionsSettingsService.SetActiveProfileBehavior;
                var setActiveProfile = (activeProfileBehavior.HasFlag(SetActiveProfileBehavior.OnRun) && (reason == UpdateProjectConfigReason.RunDebugLaunch && applyProfileFix)) || (activeProfileBehavior.HasFlag(SetActiveProfileBehavior.OnTreeChanged) && reason == UpdateProjectConfigReason.TreeChange); //applyprofilefix is set on first run only when we are added, if this happens due to a tree change but the behavior is OnRun we want to set the active profile.  Note as this only happens on first run it is not the same as setting it active on every tree change


                projectServices.ThreadingPolicy.ExecuteSynchronously(async () =>
                {
                    
                    if (applyProfileFix){
                        var activeProfile = launchSettingsProvider.ActiveProfile?.Name;
                        if (! String.IsNullOrWhiteSpace( activeProfile))
                            await launchSettingsProvider.SetActiveProfileAsync(activeProfile);
                    }

                    await launchSettingsProvider.AddOrUpdateProfileAsync(writableLaunchProfile, addToFront: false);
                    if (setActiveProfile) 
                        await launchSettingsProvider.SetActiveProfileAsync(writableLaunchProfile.Name);

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
    public class WritableLaunchProfile : ILaunchProfile //must be public to avoid having to declare our dynamic assembly a friend
#if VS17 && ! DYNAMIC_VSProjectManaged
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
#if DYNAMIC_VSProjectManaged
        private static Func<ILaunchProfile, bool> LaunchProfileIsDoNotPersistFunc;
#endif
        private static Lazy<Type> IPersistOptionType = new Lazy<Type>(() => typeof(ILaunchProfile).Assembly.GetType("Microsoft.VisualStudio.ProjectSystem.Debug.IPersistOption"));
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
#if DYNAMIC_VSProjectManaged
            if (LaunchProfileIsDoNotPersistFunc == null)
            {
                if (IPersistOptionType.Value == null)
                    LaunchProfileIsDoNotPersistFunc = (_) => false;
                else
                {
                    var instanceParam = Expression.Parameter(typeof(ILaunchProfile));
                    var asIPersist = Expression.TypeAs(instanceParam, IPersistOptionType.Value);
                    var expr = Expression.Condition(Expression.Equal(asIPersist, Expression.Constant(null)), Expression.Constant(false), Expression.Property(asIPersist, nameof(DoNotPersist)));
                    LaunchProfileIsDoNotPersistFunc = Expression.Lambda<Func<ILaunchProfile, bool>>(expr, instanceParam).Compile();
                }
            }
            DoNotPersist = LaunchProfileIsDoNotPersistFunc(launchProfile);

#else
            if (launchProfile is IPersistOption persistOptionLaunchProfile)
            {
                // IPersistOption
                DoNotPersist = persistOptionLaunchProfile.DoNotPersist;
            }
#endif
#endif
        }

        private static Func<ILaunchProfile, WritableLaunchProfile> getWritableProfileFunc;
        internal static WritableLaunchProfile GetWritableLaunchProfile(ILaunchProfile profile)
        {
#if DYNAMIC_VSProjectManaged
            if (getWritableProfileFunc == null && IPersistOptionType.Value != null)
            {
                var ourType = typeof(WritableLaunchProfile);
                var asmName = new AssemblyName() { Name = "SmartCLIArgsDynamicAsm" };
                asmName.SetPublicKey(ourType.Assembly.GetName().GetPublicKey());
                var assemBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);

                var classBuilder = assemBuilder.DefineDynamicModule("SmartCLIArgsDynamicMod").DefineType("DynamicWritableLaunchProfile", TypeAttributes.NotPublic | TypeAttributes.Class, ourType);
                classBuilder.AddInterfaceImplementation(IPersistOptionType.Value);
                // not sure why  AssemblyBuilder is a baby true IL code doesn't define interface impelmentations that are just inherited
                var persist_get = classBuilder.DefineMethod("get_" + nameof(DoNotPersist), MethodAttributes.Virtual | MethodAttributes.Public, typeof(bool), Type.EmptyTypes);
                var il = persist_get.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Callvirt, ourType.GetMethod(persist_get.Name), null);
                il.Emit(OpCodes.Ret);


                classBuilder.DefineMethodOverride(persist_get, IPersistOptionType.Value.GetMethod(persist_get.Name));

                var constructorArgTypes = new[] { typeof(ILaunchProfile) };
                var constructor = classBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, constructorArgTypes);
                var baseConstructor = ourType.GetConstructor(constructorArgTypes);
                il = constructor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, baseConstructor);
                il.Emit(OpCodes.Nop);
                il.Emit(OpCodes.Nop);
                il.Emit(OpCodes.Ret);
                var DynamicWritableLaunchProfileType = classBuilder.CreateType();
                var constructorInfo = DynamicWritableLaunchProfileType.GetConstructor(constructorArgTypes);
                var instanceParam = Expression.Parameter(typeof(ILaunchProfile));
                var expr = Expression.TypeAs(Expression.New(constructorInfo, instanceParam), ourType);
                getWritableProfileFunc = Expression.Lambda<Func<ILaunchProfile, WritableLaunchProfile>>(expr, instanceParam).Compile();

            }
            if (IPersistOptionType.Value != null)
                return getWritableProfileFunc(profile);
#endif
            return new WritableLaunchProfile(profile);


        }

    }

}
