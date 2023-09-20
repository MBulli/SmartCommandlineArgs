//------------------------------------------------------------------------------
// <copyright file="CmdArgsPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using EnvDTE;
using Newtonsoft.Json;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using SmartCmdArgs.ViewModel;
using SmartCmdArgs.Services;
using System.Threading;

using Task = System.Threading.Tasks.Task;
using IServiceProvider = System.IServiceProvider;

namespace SmartCmdArgs
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "2.6.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow), Window = ToolWindow.ToolWindowGuidString)]
    [ProvideOptionPage(typeof(CmdArgsOptionPage), "Smart Command Line Arguments", "General", 1000, 1001, false)]
    [ProvideBindingPath]
    [ProvideKeyBindingTable(ToolWindow.ToolWindowGuidString, 200)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(CmdArgsPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class CmdArgsPackage : AsyncPackage, INotifyPropertyChanged
    {
        /// <summary>
        /// CmdArgsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "131b0c0a-5dd0-4680-b261-86ab5387b86e";
        public const string DataObjectCmdJsonFormat = "SmartCommandlineArgs_D11D715E-CBF3-43F2-A1C1-168FD5C48505";
        public const string DataObjectCmdListFormat = "SmartCommandlineArgs_35AD7E71-E0BC-4440-97D9-2E6DA3085BE4";
        public const string SolutionOptionKey = "SmartCommandlineArgsVA"; // Only letters are allowed

        private readonly Regex msBuildPropertyRegex = new Regex(@"\$\((?<propertyName>(?:(?!\$\()[^)])*?)\)", RegexOptions.Compiled);

        private IVisualStudioHelperService vsHelper;
        private IFileStorageService fileStorage;
        private IOptionsSettingsService optionsSettings;
        private IViewModelUpdateService projectUpdateService;
        private ISuoDataService suoDataService;

        public ToolWindowViewModel ToolWindowViewModel { get; }

        public static CmdArgsPackage Instance { get; private set; }

        public IServiceProvider ServiceProvider {  get; private set; }

        // this is needed to keep the saved value in the suo file at null
        // if the user does not explicitly enable or disable the extension
        private bool? _isEnabledSaved;
        public bool? IsEnabledSaved
        {
            get => _isEnabledSaved;
            set {
                _isEnabledSaved = value;
                IsEnabled = value ?? optionsSettings.EnabledByDefault;
            }
        }

        /// <summary>
        /// While the extension is disabled we do nothing.
        /// The user is asked to enable the extension.
        /// This solves the issue that the extension accidentilly overrides user changes.
        /// 
        /// If this changes the updated value is not written to the suo file.
        /// For that `IsEnabledSaved` has to be updated.
        /// </summary>
        private bool _isEnabled;
        public bool IsEnabled {
            get => _isEnabled;
            private set {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    IsEnabledChanged();
                }
            }
        }

        public bool SettingsLoaded { get; private set; } = false;

        public bool IsSolutionOpen => vsHelper.IsSolutionOpen;

        private readonly Debouncer _updateIsActiveDebouncer;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow"/> class.
        /// </summary>
        public CmdArgsPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.

            Debug.Assert(Instance == null, "There can be only be one! (Package)");
            Instance = this;

            ToolWindowViewModel = new ToolWindowViewModel(this);

            // add option keys to store custom data in suo file
            this.AddOptionKey(SolutionOptionKey);

            _updateIsActiveDebouncer = new Debouncer(TimeSpan.FromMilliseconds(250), UpdateIsActiveForArguments);

            ServiceProvider = ConfigureServices();

            vsHelper = ServiceProvider.GetRequiredService<IVisualStudioHelperService>();
            fileStorage = ServiceProvider.GetRequiredService<IFileStorageService>();
            optionsSettings = ServiceProvider.GetRequiredService<IOptionsSettingsService>();
            projectUpdateService = ServiceProvider.GetRequiredService<IViewModelUpdateService>();
            suoDataService = ServiceProvider.GetRequiredService<ISuoDataService>();
        }

        protected override void Dispose(bool disposing)
        {
            _updateIsActiveDebouncer?.Dispose();
            ToolWindowViewModel.Dispose();

            base.Dispose(disposing);
        }

        private struct EnvVar
        {
            public string Name;
            public string Value;
        }

        private bool TryParseEnvVar(string str, out EnvVar envVar)
        {
            var parts = str.Split(new[] { '=' }, 2);

            if (parts.Length == 2)
            {
                envVar = new EnvVar { Name = parts[0], Value = parts[1] };
                return true;
            }

            envVar = new EnvVar();
            return false;
        }

        #region IsActive Management for Items
        private ISet<CmdArgument> GetAllActiveItemsForProject(IVsHierarchy project)
        {
            if (!optionsSettings.ManageCommandLineArgs
                && !optionsSettings.ManageEnvironmentVars
                && !optionsSettings.ManageWorkingDirectories)
            {
                return new HashSet<CmdArgument>();
            }

            var Args = new HashSet<CmdArgument>();
            var EnvVars = new Dictionary<string, CmdArgument>();
            CmdArgument workDir = null;

            foreach (var item in GetAllComamndLineItemsForProject(project))
            {
                if (item.ArgumentType == ArgumentType.CmdArg && optionsSettings.ManageCommandLineArgs)
                {
                    Args.Add(item);
                }
                else if (item.ArgumentType == ArgumentType.EnvVar && optionsSettings.ManageEnvironmentVars)
                {
                    if (TryParseEnvVar(item.Value, out EnvVar envVar))
                    {
                        EnvVars[envVar.Name] = item;
                    }
                }
                else if (item.ArgumentType == ArgumentType.WorkDir && optionsSettings.ManageWorkingDirectories)
                {
                    workDir = item;
                }
            }

            var result = new HashSet<CmdArgument>(Args.Concat(EnvVars.Values));

            if (workDir != null)
            {
                result.Add(workDir);
            }

            return result;
        }

        private void UpdateIsActiveForArguments()
        {
            foreach (var cmdProject in ToolWindowViewModel.TreeViewModel.AllProjects)
            {
                if (optionsSettings.DisableInactiveItems == InactiveDisableMode.InAllProjects
                    || (optionsSettings.DisableInactiveItems != InactiveDisableMode.Disabled && cmdProject.IsStartupProject))
                {
                    var project = vsHelper.HierarchyForProjectGuid(cmdProject.Id);
                    var activeItems = GetAllActiveItemsForProject(project);

                    foreach (var item in cmdProject.AllArguments)
                    {
                        item.IsActive = activeItems.Contains(item);
                    }
                }
                else
                {
                    foreach (var item in cmdProject.AllArguments)
                    {
                        item.IsActive = true;
                    }
                }
            }
        }

        private void UpdateIsActiveForArgumentsDebounced()
        {
            _updateIsActiveDebouncer.CallActionDebounced();
        }
        #endregion

        #region Package Members
        internal Interface GetService<Service, Interface>()
        {
            return (Interface)base.GetService(typeof(Service));
        }

        internal async Task<Interface> GetServiceAsync<Service, Interface>()
        {
            return (Interface)await base.GetServiceAsync(typeof(Service));
        }

        internal Page GetDialogPage<Page>()
            where Page : class
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetDialogPage(typeof(Page)) as Page;
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await Commands.InitializeAsync(this);

            await InitializeAsyncServices();

            // we want to know about changes to the solution state even if the extension is disabled
            // so we can update our interface
            vsHelper.SolutionAfterOpen += VsHelper_SolutionOpend;
            vsHelper.SolutionBeforeClose += VsHelper_SolutionWillClose;
            vsHelper.SolutionAfterClose += VsHelper_SolutionClosed;

            // has to be registered here to listen to settings changes even if the extension is disabled
            // so we can reload them if neccessary to give the user the correct values if he wants to enable the extension
            fileStorage.FileStorageChanged += FileStorage_FileStorageChanged;

            // Switch to main thread
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            // Extension window was opend while a solution is already open
            if (IsSolutionOpen)
            {
                Logger.Info("Package.Initialize called while solution was already open.");

                InitializeConfigForSolution();
            }

            UpdateDisabledScreen();

            ToolWindowViewModel.UseMonospaceFont = optionsSettings.UseMonospaceFont;
            ToolWindowViewModel.DisplayTagForCla = optionsSettings.DisplayTagForCla;

            await base.InitializeAsync(cancellationToken, progress);
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IProjectConfigService, ProjectConfigService>();
            services.AddSingleton<IVisualStudioHelperService, VisualStudioHelperService>();
            services.AddSingleton<IFileStorageService, FileStorageService>();
            services.AddSingleton<IOptionsSettingsService, OptionsSettingsService>();
            services.AddSingleton<IViewModelUpdateService, ViewModelUpdateService>();
            services.AddSingleton<ISuoDataService, SuoDataService>();
            services.AddSingleton<IItemPathService, ItemPathService>();

            var asyncInitializableServices = services
                .Where(x => x.Lifetime == ServiceLifetime.Singleton)
                .Where(x => typeof(IAsyncInitializable).IsAssignableFrom(x.ImplementationType))
                .ToList();

            foreach (var service in asyncInitializableServices)
            {
                services.AddSingleton(x => x.GetRequiredService(service.ServiceType) as IAsyncInitializable);
            }

            return services.BuildServiceProvider();
        }

        private async Task InitializeAsyncServices()
        {
            var initializableServices = ServiceProvider.GetServices<IAsyncInitializable>();
            foreach (var service in initializableServices)
            {
                await service.InitializeAsync();
            }
        }

        private void OptionsSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!SettingsLoaded)
                return;

            switch (e.PropertyName)
            {
                case nameof(IOptionsSettingsService.SaveSettingsToJson): SaveSettingsToJsonChanged(); break;
                case nameof(IOptionsSettingsService.UseCustomJsonRoot): UseCustomJsonRootChanged(); break;
                case nameof(IOptionsSettingsService.JsonRootPath): JsonRootPathChanged(); break;
                case nameof(IOptionsSettingsService.VcsSupportEnabled): VcsSupportChanged(); break;
                case nameof(IOptionsSettingsService.UseSolutionDir): UseSolutionDirChanged(); break;
                case nameof(IOptionsSettingsService.ManageCommandLineArgs): UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageEnvironmentVars): UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageWorkingDirectories): UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.UseMonospaceFont): UseMonospaceFontChanged(); break;
                case nameof(IOptionsSettingsService.DisplayTagForCla): DisplayTagForClaChanged(); break;
                case nameof(IOptionsSettingsService.DisableInactiveItems): UpdateIsActiveForArgumentsDebounced(); break;
            }
        }

        private void OnTreeContentChangedThrottled(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            if (optionsSettings.VcsSupportEnabled)
            {
                Logger.Info($"Tree content changed and VCS support is enabled. Saving all project commands to json file for project '{e.AffectedProject.Id}'.");

                var projectGuid = e.AffectedProject.Id;

                try
                {
                    var project = vsHelper.HierarchyForProjectGuid(projectGuid);
                    fileStorage.SaveProject(project);
                }
                catch (Exception ex)
                {
                    string msg = $"Failed to save json for project '{projectGuid}' with error: {ex}";
                    Logger.Error(msg);
                    MessageBox.Show(msg);
                }
            }
        }

        private void OnTreeChangedThrottled(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            var projectGuid = e.AffectedProject.Id;
            var project = vsHelper.HierarchyForProjectGuid(projectGuid);
            UpdateConfigurationForProject(project);
        }

        private void OnTreeChanged(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            UpdateIsActiveForArgumentsDebounced();
        }

        private void AttachToEvents()
        {
            // events registered here are only called while the extension is enabled

            vsHelper.StartupProjectChanged += VsHelper_StartupProjectChanged;
            vsHelper.ProjectConfigurationChanged += VsHelper_ProjectConfigurationChanged;
            vsHelper.ProjectBeforeRun += VsHelper_ProjectWillRun;
            vsHelper.LaunchProfileChanged += VsHelper_LaunchProfileChanged;

            vsHelper.ProjectAfterOpen += VsHelper_ProjectAdded;
            vsHelper.ProjectBeforeClose += VsHelper_ProjectRemoved;
            vsHelper.ProjectAfterRename += VsHelper_ProjectRenamed;
            vsHelper.ProjectAfterLoad += VsHelper_ProjectAfterLoad;

            optionsSettings.PropertyChanged += OptionsSettings_PropertyChanged;

            ToolWindowViewModel.TreeViewModel.ItemSelectionChanged += OnItemSelectionChanged;
            ToolWindowViewModel.TreeViewModel.TreeContentChangedThrottled += OnTreeContentChangedThrottled;
            ToolWindowViewModel.TreeViewModel.TreeChangedThrottled += OnTreeChangedThrottled;
            ToolWindowViewModel.TreeViewModel.TreeChanged += OnTreeChanged;
        }

        private void DetachFromEvents()
        {
            // all events regitered in AttachToEvents should be unregisterd here

            vsHelper.StartupProjectChanged -= VsHelper_StartupProjectChanged;
            vsHelper.ProjectConfigurationChanged -= VsHelper_ProjectConfigurationChanged;
            vsHelper.ProjectBeforeRun -= VsHelper_ProjectWillRun;
            vsHelper.LaunchProfileChanged -= VsHelper_LaunchProfileChanged;

            vsHelper.ProjectAfterOpen -= VsHelper_ProjectAdded;
            vsHelper.ProjectBeforeClose -= VsHelper_ProjectRemoved;
            vsHelper.ProjectAfterRename -= VsHelper_ProjectRenamed;
            vsHelper.ProjectAfterLoad -= VsHelper_ProjectAfterLoad;

            optionsSettings.PropertyChanged -= OptionsSettings_PropertyChanged;

            ToolWindowViewModel.TreeViewModel.ItemSelectionChanged -= OnItemSelectionChanged;
            ToolWindowViewModel.TreeViewModel.TreeContentChangedThrottled -= OnTreeContentChangedThrottled;
            ToolWindowViewModel.TreeViewModel.TreeChangedThrottled -= OnTreeChangedThrottled;
            ToolWindowViewModel.TreeViewModel.TreeChanged -= OnTreeChanged;
        }

        private void UpdateDisabledScreen()
        {
            ToolWindowViewModel.ShowDisabledScreen = !IsEnabled && IsSolutionOpen;
        }

        private void OnItemSelectionChanged(object sender, CmdBase cmdBase)
        {
            vsHelper.UpdateShellCommandUI(false);
        }

        protected override WindowPane InstantiateToolWindow(Type toolWindowType)
        {
            if (toolWindowType == typeof(ToolWindow))
                return new ToolWindow(ToolWindowViewModel) { Package = this };
            else
                return base.InstantiateToolWindow(toolWindowType);
        }

        protected override void OnLoadOptions(string key, Stream stream)
        {
            base.OnLoadOptions(key, stream);

            if (key == SolutionOptionKey)
            {
                suoDataService.LoadFromStream(stream);
            }
        }

        protected override void OnSaveOptions(string key, Stream stream)
        {
            base.OnSaveOptions(key, stream);
            if (key == SolutionOptionKey)
            {
                if (IsEnabled)
                    suoDataService.Update();

                suoDataService.SaveToStream(stream);
            }
        }

        public void SetAsStartupProject(Guid guid)
        {
            vsHelper.SetNewStartupProject(vsHelper.GetUniqueName(vsHelper.HierarchyForProjectGuid(guid)));
        }

        #endregion

        private void UpdateConfigurationForProject(IVsHierarchy project)
        {
            if (!IsEnabled || project == null)
                return;

            var commandLineArgs = optionsSettings.ManageCommandLineArgs ? CreateCommandLineArgsForProject(project) : null;
            var envVars = optionsSettings.ManageEnvironmentVars ? GetEnvVarsForProject(project) : null;
            var workDir = optionsSettings.ManageWorkingDirectories ? GetWorkDirForProject(project) : null;

            if (commandLineArgs is null && envVars is null && workDir is null)
                return;

            ServiceProvider.GetRequiredService<IProjectConfigService>().SetConfig(project, commandLineArgs, envVars, workDir);
            Logger.Info($"Updated Configuration for Project: {project.GetName()}");
        }

        public IVsHierarchy GetProjectForArg(CmdBase cmdBase)
        {
            var projectGuid = cmdBase.ProjectGuid;

            if (projectGuid == Guid.Empty)
                return null;

            return vsHelper.HierarchyForProjectGuid(projectGuid);
        }

        public string EvaluateMacros(string arg, IVsHierarchy project)
        {
            if (!optionsSettings.MacroEvaluationEnabled)
                return arg;

            if (project == null)
                return arg;

            return msBuildPropertyRegex.Replace(arg,
                match => vsHelper.GetMSBuildPropertyValueForActiveConfig(project, match.Groups["propertyName"].Value) ?? match.Value);
        }

        private TResult AggregateComamndLineItemsForProject<TResult>(IVsHierarchy project, Func<IEnumerable<CmdBase>, Func<CmdContainer, TResult>, CmdContainer, TResult> joinItems)
        {
            if (project == null)
                return default;

            var projectCmd = ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(project.GetGuid());
            if (projectCmd == null)
                return default;

            var projectObj = project.GetProject();

            string projConfig = projectObj?.ConfigurationManager?.ActiveConfiguration?.ConfigurationName;
            string projPlatform = projectObj?.ConfigurationManager?.ActiveConfiguration?.PlatformName;

            string activeLaunchProfile = null;
            if (project.IsCpsProject())
                activeLaunchProfile = CpsProjectSupport.GetActiveLaunchProfileName(projectObj);

            TResult JoinContainer(CmdContainer con)
            {
                IEnumerable<CmdBase> items = con.Items
                    .Where(x => x.IsChecked != false);

                if (projConfig != null)
                    items = items.Where(x => { var conf = x.UsedProjectConfig; return conf == null || conf == projConfig; });

                if (projPlatform != null)
                    items = items.Where(x => { var plat = x.UsedProjectPlatform; return plat == null || plat == projPlatform; });

                if (activeLaunchProfile != null)
                    items = items.Where(x => { var prof = x.UsedLaunchProfile; return prof == null || prof == activeLaunchProfile; });

                return joinItems(items, JoinContainer, con);
            }

            return JoinContainer(projectCmd);
        }

        private IEnumerable<CmdArgument> GetAllComamndLineItemsForProject(IVsHierarchy project)
        {
            IEnumerable<CmdArgument> joinItems(IEnumerable<CmdBase> items, Func<CmdContainer, IEnumerable<CmdArgument>> joinContainer, CmdContainer parentContainer)
            {
                foreach (var item in items)
                {
                    if (item is CmdContainer con)
                    {
                        foreach (var child in joinContainer(con))
                            yield return child;
                    }
                    else if (item is CmdArgument arg)
                    {
                        yield return arg;
                    }
                }
            }

            return AggregateComamndLineItemsForProject<IEnumerable<CmdArgument>>(project, joinItems);
        }

        private string CreateCommandLineArgsForProject(IVsHierarchy project)
        {
            return AggregateComamndLineItemsForProject<string>(project,
                (items, joinContainer, parentContainer) =>
                {
                    var strings = items
                        .Where(x => !(x is CmdArgument arg) || arg.ArgumentType == ArgumentType.CmdArg)
                        .Select(x => x is CmdContainer c ? joinContainer(c) : EvaluateMacros(x.Value, project))
                        .Where(x => !string.IsNullOrEmpty(x));

                    var joinedString = string.Join(parentContainer.Delimiter, strings);

                    return joinedString != string.Empty
                        ? parentContainer.Prefix + joinedString + parentContainer.Postfix
                        : string.Empty;
                });
        }

        private IDictionary<string, string> GetEnvVarsForProject(IVsHierarchy project)
        {
            var result = new Dictionary<string, string>();

            foreach (var item in GetAllComamndLineItemsForProject(project))
            {
                if (item.ArgumentType != ArgumentType.EnvVar) continue;

                if (TryParseEnvVar(item.Value, out EnvVar envVar))
                {
                    result[envVar.Name] = EvaluateMacros(envVar.Value, project);
                }
            }

            return result;
        }

        private string GetWorkDirForProject(IVsHierarchy project)
        {
            var result = "";

            foreach (var item in GetAllComamndLineItemsForProject(project))
            {
                if (item.ArgumentType != ArgumentType.WorkDir) continue;

                result = EvaluateMacros(item.Value, project);
            }

            return result;
        }

        public string CreateCommandLineArgsForProject(Guid guid)
        {
            return CreateCommandLineArgsForProject(vsHelper.HierarchyForProjectGuid(guid));
        }

        public IDictionary<string, string> GetEnvVarsForProject(Guid guid)
        {
            return GetEnvVarsForProject(vsHelper.HierarchyForProjectGuid(guid));
        }

        public List<string> GetProjectConfigurations(Guid projGuid)
        {
            IVsHierarchy project = vsHelper.HierarchyForProjectGuid(projGuid);

            var configs = (project.GetProject()?.ConfigurationManager?.ConfigurationRowNames as Array)?.Cast<string>().ToList();
            return configs ?? new List<string>();
        }

        public List<string> GetProjectPlatforms(Guid projGuid)
        {
            IVsHierarchy project = vsHelper.HierarchyForProjectGuid(projGuid);

            var platforms = (project.GetProject()?.ConfigurationManager?.PlatformNames as Array)?.Cast<string>().ToList();
            return platforms ?? new List<string>();
        }

        public List<string> GetLaunchProfiles(Guid projGuid)
        {
            IVsHierarchy project = vsHelper.HierarchyForProjectGuid(projGuid);

            List<string> launchProfiles = null;
            if (project?.IsCpsProject() == true)
            {
                launchProfiles = CpsProjectSupport.GetLaunchProfileNames(project.GetProject())?.ToList();
            }

            return launchProfiles ?? new List<string>();
        }

        private void FileStorage_FileStorageChanged(object sender, FileStorageChangedEventArgs e)
        {
            // This event is triggered on non-main thread!

            Logger.Info($"Dispatching update commands function call");

            JoinableTaskFactory.RunAsync(async delegate
            {
                // git branch and merge might lead to a race condition here.
                // If a branch is checkout where the json file differs, the
                // filewatcher will trigger an event which is dispatched here.
                // However, while the function call is queued VS may reopen the
                // solution due to changes. This will ultimately result in a
                // null ref exception because the project object is unloaded.
                // UpdateCommandsForProject() will skip such projects because
                // their guid is empty.

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (e.Type == FileStorageChanedType.Settings)
                {
                    if (optionsSettings.SaveSettingsToJson)
                        LoadSettings();

                    return;
                }

                if (!IsEnabled)
                    return;

                if (e.IsSolutionWide != optionsSettings.UseSolutionDir)
                    return;

                if (!optionsSettings.VcsSupportEnabled)
                    return;

                ToolWindowHistory.SaveState();

                IEnumerable<IVsHierarchy> projects;
                if (e.IsSolutionWide)
                {
                    Logger.Info($"Dispatched update commands function calls for the solution.");
                    projects = vsHelper.GetSupportedProjects();
                }
                else
                {
                    Logger.Info($"Dispatched update commands function call for project '{e.Project.GetDisplayName()}'");
                    projects = new[] { e.Project };
                }

                foreach (var project in projects)
                {
                    if (project.GetGuid() == Guid.Empty)
                    {
                        Logger.Info($"Race condition might occurred while dispatching update commands function call. Project is already unloaded.");
                    }

                    projectUpdateService.UpdateCommandsForProject(project);
                }
            });
        }

        public void SaveSettings()
        {
            fileStorage.SaveSettings();
        }

        public void LoadSettings()
        {
            var settings = fileStorage.ReadSettings();

            var areSettingsFromFile = settings != null;

            if (settings == null)
                settings = suoDataService.SuoDataJson.Settings;

            if (settings == null)
                settings = new SettingsJson();

            var vm = ToolWindowViewModel.SettingsViewModel;

            vm.Assign(settings);
            vm.SaveSettingsToJson = areSettingsFromFile;
        }

        private void IsEnabledChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));

            UpdateDisabledScreen();

            if (IsEnabled)
            {
                AttachToEvents();
                InitializeDataForSolution();
            }
            else
            {
                // captures the state right after disableing the extension
                // changes after this point are ignored
                suoDataService.Update();
                DetachFromEvents();
                FinalizeDataForSolution();
            }
        }

        private void InitializeConfigForSolution()
        {
            suoDataService.Deserialize();

            LoadSettings();
            SettingsLoaded = true;

            IsEnabledSaved = suoDataService.SuoDataJson.IsEnabled;
        }

        private void InitializeDataForSolution()
        {
            Debug.Assert(IsEnabled);

            ToolWindowViewModel.TreeViewModel.ShowAllProjects = suoDataService.SuoDataJson.ShowAllProjects;

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                projectUpdateService.UpdateCommandsForProject(project);
                fileStorage.AddProject(project);
            }
            UpdateCurrentStartupProject();
            UpdateIsActiveForArgumentsDebounced();
        }

        private void FinalizeDataForSolution()
        {
            Debug.Assert(!IsEnabled);

            fileStorage.RemoveAllProjects();
            ToolWindowViewModel.Reset();
        }

        private void FinalizeConfigForSolution()
        {
            IsEnabled = false;
            UpdateDisabledScreen();
            suoDataService.Reset();
            SettingsLoaded = false;
        }

        #region VS Events
        private void VsHelper_SolutionOpend(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution opened.");

            UpdateDisabledScreen();
            InitializeConfigForSolution();
        }

        private void VsHelper_SolutionWillClose(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution will close.");

            fileStorage.RemoveAllProjects();
        }

        private void VsHelper_SolutionClosed(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution closed.");

            FinalizeConfigForSolution();
        }

        private void VsHelper_StartupProjectChanged(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: startup project changed.");

            UpdateCurrentStartupProject();
        }

        private void VsHelper_ProjectConfigurationChanged(object sender, IVsHierarchy vsHierarchy)
        {
            Logger.Info("VS-Event: Project configuration changed.");

            UpdateIsActiveForArgumentsDebounced();
        }

        private void VsHelper_LaunchProfileChanged(object sender, IVsHierarchy e)
        {
            Logger.Info("VS-Event: Project launch profile changed.");

            UpdateIsActiveForArgumentsDebounced();
        }

        private void VsHelper_ProjectWillRun(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Startup project will run.");

            foreach (var startupProject in ToolWindowViewModel.TreeViewModel.StartupProjects)
            {
                var project = vsHelper.HierarchyForProjectGuid(startupProject.Id);
                UpdateConfigurationForProject(project);
                fileStorage.SaveProject(project);
            }
        }

        private void VsHelper_ProjectAdded(object sender, ProjectAfterOpenEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' added. (IsLoadProcess={e.IsLoadProcess}, IsSolutionOpenProcess={e.IsSolutionOpenProcess})");

            if (e.IsSolutionOpenProcess)
                return;

            ToolWindowHistory.SaveState();

            projectUpdateService.UpdateCommandsForProject(e.Project);
            fileStorage.AddProject(e.Project);
        }

        private void VsHelper_ProjectRemoved(object sender, ProjectBeforeCloseEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' removed. (IsUnloadProcess={e.IsUnloadProcess}, IsSolutionCloseProcess={e.IsSolutionCloseProcess})");

            if (e.IsSolutionCloseProcess)
                return;

            fileStorage.SaveProject(e.Project);

            ToolWindowViewModel.TreeViewModel.Projects.Remove(e.Project.GetGuid());

            fileStorage.RemoveProject(e.Project);
        }

        private void VsHelper_ProjectRenamed(object sender, ProjectAfterRenameEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.OldProjectName}' renamed to '{e.Project.GetName()}'.");

            fileStorage.RenameProject(e.Project, e.OldProjectDir, e.OldProjectName);

            ToolWindowViewModel.RenameProject(e.Project);
        }

        private void VsHelper_ProjectAfterLoad(object sender, IVsHierarchy e)
        {
            Logger.Info("VS-Event: Project loaded.");

            // Startup project must be set here beacuase in the event of a project
            // reload the StartupProjectChanged event is fired before the project
            // is added so we don't know it and can't set it as startup project
            UpdateCurrentStartupProject();
        }
        #endregion

        #region OptionPage Events
        private void SaveSettingsToJsonChanged()
        {
            SaveSettings();
        }

        private void UseCustomJsonRootChanged()
        {
            fileStorage.SaveAllProjects();
        }

        private void JsonRootPathChanged()
        {
            fileStorage.SaveAllProjects();
        }

        private void VcsSupportChanged()
        {
            if (!optionsSettings.VcsSupportEnabled)
                return;

            ToolWindowHistory.SaveState();

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                projectUpdateService.UpdateCommandsForProject(project);
            }
            fileStorage.SaveAllProjects();
        }

        private void UseMonospaceFontChanged()
        {
            ToolWindowViewModel.UseMonospaceFont = optionsSettings.UseMonospaceFont;
        }

        private void DisplayTagForClaChanged()
        {
            ToolWindowViewModel.DisplayTagForCla = optionsSettings.DisplayTagForCla;
        }

        private void UseSolutionDirChanged()
        {
            fileStorage.DeleteAllUnusedArgFiles();
            fileStorage.SaveAllProjects();
        }

        #endregion

        private void UpdateCurrentStartupProject()
        {
            var startupProjectGuids = new HashSet<Guid>(vsHelper.StartupProjectUniqueNames()
                .Select(vsHelper.HierarchyForProjectName).Select(hierarchy => hierarchy.GetGuid()));

            ToolWindowViewModel.TreeViewModel.Projects.ForEach(p => p.Value.IsStartupProject = startupProjectGuids.Contains(p.Key));
            ToolWindowViewModel.TreeViewModel.UpdateTree();
        }

        public Task OpenFileInVisualStudioAsync(string path) => vsHelper.OpenFileInVisualStudioAsync(path);
    }
}
