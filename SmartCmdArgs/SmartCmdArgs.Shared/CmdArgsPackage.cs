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
using ServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;

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

        private IVisualStudioHelperService vsHelper;
        private IFileStorageService fileStorage;
        private IOptionsSettingsService optionsSettings;
        private IViewModelUpdateService viewModelUpdateService;
        private ISuoDataService suoDataService;
        private IItemAggregationService itemAggregationService;
        private ISettingsService settingsService;

        public ToolWindowViewModel ToolWindowViewModel { get; }

        public static CmdArgsPackage Instance { get; private set; }

        private ServiceProvider serviceProvider;
        public IServiceProvider ServiceProvider => serviceProvider;

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

        public bool IsSolutionOpen => vsHelper.IsSolutionOpen;

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

            // add option keys to store custom data in suo file
            this.AddOptionKey(SolutionOptionKey);

            serviceProvider = ConfigureServices();

            ToolWindowViewModel = ServiceProvider.GetRequiredService<ToolWindowViewModel>();

            vsHelper = ServiceProvider.GetRequiredService<IVisualStudioHelperService>();
            fileStorage = ServiceProvider.GetRequiredService<IFileStorageService>();
            optionsSettings = ServiceProvider.GetRequiredService<IOptionsSettingsService>();
            viewModelUpdateService = ServiceProvider.GetRequiredService<IViewModelUpdateService>();
            suoDataService = ServiceProvider.GetRequiredService<ISuoDataService>();
            itemAggregationService = ServiceProvider.GetRequiredService<IItemAggregationService>();
            settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        }

        protected override void Dispose(bool disposing)
        {
            serviceProvider.Dispose();

            base.Dispose(disposing);
        }

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

        private ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton(x => GetDialogPage<CmdArgsOptionPage>());
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<ToolWindowViewModel>();
            services.AddSingleton<IProjectConfigService, ProjectConfigService>();
            services.AddSingleton<IVisualStudioHelperService, VisualStudioHelperService>();
            services.AddSingleton<IFileStorageService, FileStorageService>();
            services.AddSingleton<IOptionsSettingsService, OptionsSettingsService>();
            services.AddSingleton<IViewModelUpdateService, ViewModelUpdateService>();
            services.AddSingleton<ISuoDataService, SuoDataService>();
            services.AddSingleton<IItemPathService, ItemPathService>();
            services.AddSingleton<IItemEvaluationService, ItemEvaluationService>();
            services.AddSingleton<IItemAggregationService, ItemAggregationService>();
            services.AddSingleton<ISettingsService, SettingsService>();

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
            if (!settingsService.Loaded)
                return;

            switch (e.PropertyName)
            {
                case nameof(IOptionsSettingsService.SaveSettingsToJson): SaveSettingsToJsonChanged(); break;
                case nameof(IOptionsSettingsService.UseCustomJsonRoot): UseCustomJsonRootChanged(); break;
                case nameof(IOptionsSettingsService.JsonRootPath): JsonRootPathChanged(); break;
                case nameof(IOptionsSettingsService.VcsSupportEnabled): VcsSupportChanged(); break;
                case nameof(IOptionsSettingsService.UseSolutionDir): UseSolutionDirChanged(); break;
                case nameof(IOptionsSettingsService.ManageCommandLineArgs): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageEnvironmentVars): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.ManageWorkingDirectories): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
                case nameof(IOptionsSettingsService.UseMonospaceFont): UseMonospaceFontChanged(); break;
                case nameof(IOptionsSettingsService.DisplayTagForCla): DisplayTagForClaChanged(); break;
                case nameof(IOptionsSettingsService.DisableInactiveItems): viewModelUpdateService.UpdateIsActiveForArgumentsDebounced(); break;
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
            viewModelUpdateService.UpdateIsActiveForArgumentsDebounced();
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

            var commandLineArgs = optionsSettings.ManageCommandLineArgs ? itemAggregationService.CreateCommandLineArgsForProject(project) : null;
            var envVars = optionsSettings.ManageEnvironmentVars ? itemAggregationService.GetEnvVarsForProject(project) : null;
            var workDir = optionsSettings.ManageWorkingDirectories ? itemAggregationService.GetWorkDirForProject(project) : null;

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
                        settingsService.Load();

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

                    viewModelUpdateService.UpdateCommandsForProject(project);
                }
            });
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

            settingsService.Load();

            IsEnabledSaved = suoDataService.SuoDataJson.IsEnabled;
        }

        private void InitializeDataForSolution()
        {
            Debug.Assert(IsEnabled);

            ToolWindowViewModel.TreeViewModel.ShowAllProjects = suoDataService.SuoDataJson.ShowAllProjects;

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                viewModelUpdateService.UpdateCommandsForProject(project);
                fileStorage.AddProject(project);
            }
            viewModelUpdateService.UpdateCurrentStartupProject();
            viewModelUpdateService.UpdateIsActiveForArgumentsDebounced();
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
            settingsService.Reset();
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

            viewModelUpdateService.UpdateCurrentStartupProject();
        }

        private void VsHelper_ProjectConfigurationChanged(object sender, IVsHierarchy vsHierarchy)
        {
            Logger.Info("VS-Event: Project configuration changed.");

            viewModelUpdateService.UpdateIsActiveForArgumentsDebounced();
        }

        private void VsHelper_LaunchProfileChanged(object sender, IVsHierarchy e)
        {
            Logger.Info("VS-Event: Project launch profile changed.");

            viewModelUpdateService.UpdateIsActiveForArgumentsDebounced();
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

            viewModelUpdateService.UpdateCommandsForProject(e.Project);
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
            viewModelUpdateService.UpdateCurrentStartupProject();
        }
        #endregion

        #region OptionPage Events
        private void SaveSettingsToJsonChanged()
        {
            settingsService.Save();
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
                viewModelUpdateService.UpdateCommandsForProject(project);
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
    }
}
