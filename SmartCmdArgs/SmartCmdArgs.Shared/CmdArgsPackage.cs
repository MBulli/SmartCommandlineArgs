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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using EnvDTE;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using SmartCmdArgs.ViewModel;
using System.Threading;

using Task = System.Threading.Tasks.Task;

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
    [InstalledProductRegistration("#110", "#112", "2.5.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow), Window = ToolWindow.ToolWindowGuidString)]
    [ProvideOptionPage(typeof(CmdArgsOptionPage), "Smart Command Line Arguments", "General", 1000, 1001, false)]
    [ProvideBindingPath]
    [ProvideKeyBindingTable(ToolWindow.ToolWindowGuidString, 200)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(CmdArgsPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class CmdArgsPackage : AsyncPackage
    {
        /// <summary>
        /// CmdArgsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "131b0c0a-5dd0-4680-b261-86ab5387b86e";
        public const string DataObjectCmdJsonFormat = "SmartCommandlineArgs_D11D715E-CBF3-43F2-A1C1-168FD5C48505";
        public const string DataObjectCmdListFormat = "SmartCommandlineArgs_35AD7E71-E0BC-4440-97D9-2E6DA3085BE4";
        public const string SolutionOptionKey = "SmartCommandlineArgsVA"; // Only letters are allowed

        private readonly Regex msBuildPropertyRegex = new Regex(@"\$\((?<propertyName>(?:(?!\$\()[^)])*?)\)", RegexOptions.Compiled);

        private VisualStudioHelper vsHelper;
        private FileStorage fileStorage;
        public ToolWindowViewModel ToolWindowViewModel { get; }

        public static CmdArgsPackage Instance { get; private set; }

        public SettingsViewModel Settings => ToolWindowViewModel.SettingsViewModel;
        public CmdArgsOptionPage Options => GetDialogPage<CmdArgsOptionPage>();

        public bool SettingsLoaded { get; private set; } = false;

        public bool SaveSettingsToJson => Settings.SaveSettingsToJson ?? Options.SaveSettingsToJson;
        public bool UseCustomJsonRoot => Settings.UseCustomJsonRoot;
        public string JsonRootPath => Settings.JsonRootPath;
        public bool IsVcsSupportEnabled => Settings.VcsSupportEnabled ?? Options.VcsSupportEnabled;
        public bool IsMacroEvaluationEnabled => Settings.MacroEvaluationEnabled ?? Options.MacroEvaluationEnabled;
        public bool IsUseSolutionDirEnabled => vsHelper?.GetSolutionFilename() != null && (Settings.UseSolutionDir ?? Options.UseSolutionDir);

        public bool IsUseMonospaceFontEnabled => Options.UseMonospaceFont;
        public bool DeleteEmptyFilesAutomatically => Options.DeleteEmptyFilesAutomatically;
        public bool DeleteUnnecessaryFilesAutomatically => Options.DeleteUnnecessaryFilesAutomatically;

        // We store the commandline arguments also in the suo file.
        // This is handled in the OnLoad/SaveOptions methods.
        // As the parser needs a initialized instance of vsHelper,
        // the json string from the suo is saved in this variable and
        // processed later.
        private string toolWindowStateFromSolutionJsonStr;
        private SuoDataJson toolWindowStateLoadedFromSolution;

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

            vsHelper = new VisualStudioHelper(this);
            vsHelper.SolutionAfterOpen += VsHelper_SolutionOpend;
            vsHelper.SolutionBeforeClose += VsHelper_SolutionWillClose;
            vsHelper.SolutionAfterClose += VsHelper_SolutionClosed;
            vsHelper.StartupProjectChanged += VsHelper_StartupProjectChanged;
            vsHelper.StartupProjectConfigurationChanged += VsHelper_StartupProjectConfigurationChanged;
            vsHelper.ProjectBeforeRun += VsHelper_ProjectWillRun;

            vsHelper.ProjectAfterOpen += VsHelper_ProjectAdded;
            vsHelper.ProjectBeforeClose += VsHelper_ProjectRemoved;
            vsHelper.ProjectAfterRename += VsHelper_ProjectRenamed;
            vsHelper.ProjectAfterLoad += VsHelper_ProjectAfterLoad;

            fileStorage = new FileStorage(this, vsHelper);
            fileStorage.FileStorageChanged += FileStorage_FileStorageChanged;

            await vsHelper.InitializeAsync();

            // Switch to main thread
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            ToolWindowViewModel.SettingsViewModel.PropertyChanged += SettingsViewModel_PropertyChanged;
            GetDialogPage<CmdArgsOptionPage>().PropertyChanged += CmdArgsOptionPage_PropertyChanged;

            // Extension window was opend while a solution is already open
            if (vsHelper.IsSolutionOpen)
            {
                Logger.Info("Package.Initialize called while solution was already open.");

                InitializeForSolution();
            }

            ToolWindowViewModel.TreeViewModel.ItemSelectionChanged += OnItemSelectionChanged;
            ToolWindowViewModel.TreeViewModel.TreeContentChangedThrottled += OnTreeContentChangedThrottled;

            ToolWindowViewModel.UseMonospaceFont = IsUseMonospaceFontEnabled;

            await base.InitializeAsync(cancellationToken, progress);
        }

        private void CmdArgsOptionPage_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!SettingsLoaded)
                return;

            switch (e.PropertyName)
            {
                case nameof(CmdArgsOptionPage.SaveSettingsToJson): SaveSettingsToJsonChanged(); break;
                case nameof(CmdArgsOptionPage.VcsSupportEnabled): VcsSupportChanged(); break;
                case nameof(CmdArgsOptionPage.UseSolutionDir): UseSolutionDirChanged(); break;
                case nameof(CmdArgsOptionPage.UseMonospaceFont): UseMonospaceFontChanged(); break;
            }
        }

        private void SettingsViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!SettingsLoaded)
                return;

            switch (e.PropertyName)
            {
                case nameof(SettingsViewModel.SaveSettingsToJson): SaveSettingsToJsonChanged(); break;
                case nameof(SettingsViewModel.UseCustomJsonRoot): UseCustomJsonRootChanged(); break;
                case nameof(SettingsViewModel.JsonRootPath): JsonRootPathChanged(); break;
                case nameof(SettingsViewModel.VcsSupportEnabled): VcsSupportChanged(); break;
                case nameof(SettingsViewModel.UseSolutionDir): UseSolutionDirChanged(); break;
            }
        }

        private void OnTreeContentChangedThrottled(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            if (IsVcsSupportEnabled)
            {
                Logger.Info($"Tree content changed and VCS support is enabled. Saving all project commands to json file for project '{e.AffectedProject.Id}'.");

                var projectGuid = e.AffectedProject.Id;

                try
                {
                    var project = vsHelper.HierarchyForProjectGuid(projectGuid);
                    fileStorage.SaveProject(project);
                }
                catch(Exception ex)
                {
                    string msg = $"Failed to save json for project '{projectGuid}' with error: {ex}";
                    Logger.Error(msg);
                    MessageBox.Show(msg);
                }
            }
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
                StreamReader sr = new StreamReader(stream); // don't free
                toolWindowStateFromSolutionJsonStr = sr.ReadToEnd();
            }
        }

        protected override void OnSaveOptions(string key, Stream stream)
        {
            base.OnSaveOptions(key, stream);
            if (key == SolutionOptionKey)
            {
                Logger.Info("Saving commands to suo file.");
                toolWindowStateLoadedFromSolution = SuoDataSerializer.Serialize(ToolWindowViewModel, stream);
                Logger.Info("All Commands saved to suo file.");
            }
        }

        public void SetAsStartupProject(Guid guid)
        {
            vsHelper.SetNewStartupProject(vsHelper.GetUniqueName(vsHelper.HierarchyForProjectGuid(guid)));
        }

        #endregion

        private void UpdateConfigurationForProject(IVsHierarchy project)
        {
            if (project == null)
                return;

            var commandLineArgs = CreateCommandLineArgsForProject(project);
            if (commandLineArgs == null)
                return;

            ProjectArguments.SetArguments(project, commandLineArgs);
            Logger.Info($"Updated Configuration for Project: {project.GetName()}");
        }

        public IVsHierarchy GetProjectForArg(CmdBase cmdBase)
        {
            var projectGuid = cmdBase.ProjectGuid;

            if (projectGuid == Guid.Empty)
                return null;

            return vsHelper.HierarchyForProjectGuid(projectGuid);
        }

        public string MakePathAbsolute(string path, IVsHierarchy project, string buildConfig = null)
        {
            switch (Options.RelativePathRoot) {
                case RelativePathRootOption.BuildTargetDirectory:
                    return MakePathAbsoluteBasedOnTargetDir(path, project, buildConfig);
                case RelativePathRootOption.ProjectDirectory:
                    return MakePathAbsoluteBasedOnProjectDir(path, project);

                default: return null;
            }
        }

        public string MakePathRelativeBasedOnSolutionDir(string path)
        {
            string baseDir = Path.GetDirectoryName(vsHelper.GetSolutionFilename());
            return PathHelper.MakeRelativePath(path, baseDir);
        }

        public string MakePathAbsoluteBasedOnSolutionDir(string path)
        {
            string baseDir = Path.GetDirectoryName(vsHelper.GetSolutionFilename());
            return PathHelper.MakePathAbsolute(path, baseDir);
        }

        public string MakePathAbsoluteBasedOnProjectDir(string path, IVsHierarchy project)
        {
            string baseDir = project?.GetProjectDir();
            return PathHelper.MakePathAbsolute(path, baseDir);
        }

        public string MakePathAbsoluteBasedOnTargetDir(string path, IVsHierarchy project, string buildConfig)
        {
            string baseDir = null;
            if (project != null)
            {
                if (string.IsNullOrEmpty(buildConfig))
                    baseDir = vsHelper.GetMSBuildPropertyValueForActiveConfig(project, "TargetDir");
                else
                    baseDir = vsHelper.GetMSBuildPropertyValue(project, "TargetDir", buildConfig);
            }

            return PathHelper.MakePathAbsolute(path, baseDir);
        }

        public string EvaluateMacros(string arg, IVsHierarchy project)
        {
            if (!IsMacroEvaluationEnabled)
                return arg;

            if (project == null)
                return arg;

            return msBuildPropertyRegex.Replace(arg,
                match => vsHelper.GetMSBuildPropertyValueForActiveConfig(project, match.Groups["propertyName"].Value) ?? match.Value);
        }

        private string CreateCommandLineArgsForProject(IVsHierarchy project)
        {
            if (project == null)
                return null;

            var projectCmd = ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(project.GetGuid());
            if (projectCmd == null)
                return null;

            string projConfig = project.GetProject()?.ConfigurationManager?.ActiveConfiguration?.ConfigurationName;
            string projPlatform = project.GetProject()?.ConfigurationManager?.ActiveConfiguration?.PlatformName;

            string activeLaunchProfile = null;
            if (project.IsCpsProject())
                activeLaunchProfile = CpsProjectSupport.GetActiveLaunchProfileName(project.GetProject());

            string JoinContainer(CmdContainer con)
            {
                IEnumerable<CmdBase> items = con.Items.Where(x => x.IsChecked != false);

                if (projConfig != null)
                    items = items.Where(x => { var conf = x.UsedProjectConfig; return conf == null || conf == projConfig; });

                if (projPlatform != null)
                    items = items.Where(x => { var plat = x.UsedProjectPlatform; return plat == null || plat == projPlatform; });

                if (activeLaunchProfile != null)
                    items = items.Where(x => { var prof = x.UsedLaunchProfile; return prof == null || prof == activeLaunchProfile; });

                return string.Join(con.Delimiter, items.Select(x => x is CmdContainer c ? JoinContainer(c) : EvaluateMacros(x.Value, project)));
            }

            return JoinContainer(projectCmd);
        }

        public string CreateCommandLineArgsForProject(Guid guid)
        {
            return CreateCommandLineArgsForProject(vsHelper.HierarchyForProjectGuid(guid));
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
                    if (SaveSettingsToJson)
                        LoadSettings();

                    return;
                }

                if (e.IsSolutionWide != IsUseSolutionDirEnabled)
                    return;

                if (!IsVcsSupportEnabled)
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

                    UpdateCommandsForProject(project);
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

            if (settings != null && !Options.SaveSettingsToJson)
                settings.SaveSettingsToJson = true;

            if (settings == null)
                settings = toolWindowStateLoadedFromSolution.Settings;

            if (settings == null)
                settings = new SettingsJson();

            var vm = ToolWindowViewModel.SettingsViewModel;

            // the order here is important
            vm.MacroEvaluationEnabled = settings.MacroEvaluationEnabled;
            vm.VcsSupportEnabled = settings.VcsSupportEnabled;
            vm.UseSolutionDir = settings.UseSolutionDir;                    // has to be done after VcsSupportEnabled because it depends on it
            vm.UseCustomJsonRoot = settings.UseCustomJsonRoot;
            vm.JsonRootPath = settings.JsonRootPath;
            vm.SaveSettingsToJson = settings.SaveSettingsToJson;            // has to be done at the end because all settings should be correctly set before to be saved to a file
        }

        private void UpdateCommandsForProject(IVsHierarchy project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            Logger.Info($"Update commands for project '{project?.GetName()}'. IsVcsSupportEnabled={IsVcsSupportEnabled}. SolutionData.Count={toolWindowStateLoadedFromSolution?.ProjectArguments?.Count}.");

            var projectGuid = project.GetGuid();
            if (projectGuid == Guid.Empty)
            {
                Logger.Info("Skipping project because guid euqals empty.");
                return;
            }

            var solutionData = toolWindowStateLoadedFromSolution ?? new SuoDataJson();

            // joins data from solution and project
            //  => overrides solution commands for a project if a project json file exists
            //  => keeps all data from the suo file for projects without a json
            //  => if we have data in our ViewModel we use this instad of the suo file

            // get project json data
            ProjectDataJson projectData = null;
            if (IsVcsSupportEnabled)
            {
                projectData = fileStorage.ReadDataForProject(project);
            }

            // project json overrides if it exists
            if (projectData != null)
            {
                Logger.Info($"Setting {projectData?.Items?.Count} commands for project '{project.GetName()}' from json-file.");

                var projectListViewModel = ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(projectGuid);

                var projHasSuoData = solutionData.ProjectArguments.ContainsKey(projectGuid);

                // update enabled state of the project json data (source prio: ViewModel > suo file)
                if (projectData.Items != null)
                {
                    var argumentDataFromProject = projectData.AllArguments;
                    var argumentDataFromLVM = projectListViewModel?.AllArguments.ToDictionary(arg => arg.Id, arg => arg);
                    foreach (var dataFromProject in argumentDataFromProject)
                    {
                        if (argumentDataFromLVM != null && argumentDataFromLVM.TryGetValue(dataFromProject.Id, out CmdArgument argFromVM))
                            dataFromProject.Enabled = argFromVM.IsChecked;
                        else if (projHasSuoData)
                            dataFromProject.Enabled = solutionData.CheckedArguments.Contains(dataFromProject.Id);
                        else
                            dataFromProject.Enabled = dataFromProject.DefaultChecked;
                    }

                    var containerDataFromProject = projectData.AllContainer;
                    var containerDataFromLVM = projectListViewModel?.AllContainer.ToDictionary(con => con.Id, con => con);
                    foreach (var dataFromProject in containerDataFromProject)
                    {
                        if (containerDataFromLVM != null && containerDataFromLVM.TryGetValue(dataFromProject.Id, out CmdContainer conFromVM))
                            dataFromProject.Expanded = conFromVM.IsExpanded;
                        else
                            dataFromProject.Expanded = solutionData.ExpandedContainer.Contains(dataFromProject.Id);
                    }

                    var itemDataFromProject = projectData.AllItems;
                    var itemDataFromLVM = projectListViewModel?.ToDictionary(item => item.Id, item => item);
                    foreach (var dataFromProject in itemDataFromProject)
                    {
                        if (itemDataFromLVM != null && itemDataFromLVM.TryGetValue(dataFromProject.Id, out CmdBase itemFromVM))
                            dataFromProject.Selected = itemFromVM.IsSelected;
                        else
                            dataFromProject.Selected = solutionData.SelectedItems.Contains(dataFromProject.Id);
                    }

                    if (projectListViewModel != null)
                    {
                        projectData.Expanded = projectListViewModel.IsExpanded;
                        projectData.Selected = projectListViewModel.IsSelected;
                    }
                    else
                    {
                        projectData.Expanded = solutionData.ExpandedContainer.Contains(projectData.Id);
                        projectData.Selected = solutionData.SelectedItems.Contains(projectData.Id);
                    }
                }
                else
                {
                    projectData = new ProjectDataJson();
                    Logger.Info($"DataCollection for project '{project.GetName()}' is null.");
                }
            }
            // if we have data in the ViewModel we keep it
            else if (ToolWindowViewModel.TreeViewModel.Projects.ContainsKey(projectGuid))
            {
                return;
            }
            else if (IsVcsSupportEnabled)
            {
                projectData = new ProjectDataJson();
                Logger.Info("Will clear all data because of missing json file and enabled VCS support.");
            }
            // we try to read the suo file data
            else if (solutionData.ProjectArguments.TryGetValue(projectGuid, out projectData))
            {
                Logger.Info($"Will use commands from suo file for project '{project.GetName()}'.");
                var argumentDataFromProject = projectData.AllArguments;
                foreach (var arg in argumentDataFromProject)
                {
                    arg.Enabled = solutionData.CheckedArguments.Contains(arg.Id);
                }

                var containerDataFromProject = projectData.AllContainer;
                foreach (var con in containerDataFromProject)
                {
                    con.Expanded = solutionData.ExpandedContainer.Contains(con.Id);
                }

                var itemDataFromProject = projectData.AllItems;
                foreach (var item in itemDataFromProject)
                {
                    item.Selected = solutionData.SelectedItems.Contains(item.Id);
                }

                projectData.Expanded = solutionData.ExpandedContainer.Contains(projectData.Id);
                projectData.Selected = solutionData.SelectedItems.Contains(projectData.Id);
            }
            else
            {
                Logger.Info($"Gathering commands from configurations for project '{project.GetName()}'.");
                // if we don't have suo file data we read cmd args from the project configs
                projectData = new ProjectDataJson();
                projectData.Items.AddRange(ReadCommandlineArgumentsFromProject(project));
            }

            // push projectData to the ViewModel
            ToolWindowViewModel.PopulateFromProjectData(project, projectData);

            Logger.Info($"Updated Commands for project '{project.GetName()}'.");
        }

        private void InitializeForSolution()
        {
            toolWindowStateLoadedFromSolution = Logic.SuoDataSerializer.Deserialize(toolWindowStateFromSolutionJsonStr, vsHelper);

            ToolWindowViewModel.TreeViewModel.ShowAllProjects = toolWindowStateLoadedFromSolution.ShowAllProjects;

            LoadSettings();

            SettingsLoaded = true;

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                UpdateCommandsForProject(project);
                fileStorage.AddProject(project);
            }
            UpdateCurrentStartupProject();
        }

        #region VS Events
        private void VsHelper_SolutionOpend(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution opened.");

            InitializeForSolution();
        }

        private void VsHelper_SolutionWillClose(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution will close.");

            fileStorage.RemoveAllProjects();
        }

        private void VsHelper_SolutionClosed(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution closed.");

            ToolWindowViewModel.Reset();
            toolWindowStateLoadedFromSolution = null;
            SettingsLoaded = false;
        }

        private void VsHelper_StartupProjectChanged(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: startup project changed.");

            UpdateCurrentStartupProject();
        }

        private void VsHelper_StartupProjectConfigurationChanged(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Startup project configuration changed.");
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

        private void VsHelper_ProjectAdded(object sender, VisualStudioHelper.ProjectAfterOpenEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' added. (IsLoadProcess={e.IsLoadProcess}, IsSolutionOpenProcess={e.IsSolutionOpenProcess})");

            if (e.IsSolutionOpenProcess)
                return;

            ToolWindowHistory.SaveState();

            UpdateCommandsForProject(e.Project);
            fileStorage.AddProject(e.Project);
        }

        private void VsHelper_ProjectRemoved(object sender, VisualStudioHelper.ProjectBeforeCloseEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' removed. (IsUnloadProcess={e.IsUnloadProcess}, IsSolutionCloseProcess={e.IsSolutionCloseProcess})");

            if (e.IsSolutionCloseProcess)
                return;

            fileStorage.SaveProject(e.Project);

            ToolWindowViewModel.TreeViewModel.Projects.Remove(e.Project.GetGuid());

            fileStorage.RemoveProject(e.Project);
        }

        private void VsHelper_ProjectRenamed(object sender, VisualStudioHelper.ProjectAfterRenameEventArgs e)
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
            if (!IsVcsSupportEnabled)
                return;

            ToolWindowHistory.SaveState();

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                UpdateCommandsForProject(project);
            }
            fileStorage.SaveAllProjects();
        }

        private void UseMonospaceFontChanged()
        {
            ToolWindowViewModel.UseMonospaceFont = IsUseMonospaceFontEnabled;
        }

        private void UseSolutionDirChanged()
        {
            fileStorage.DeleteAllUnusedArgFiles();
            fileStorage.SaveAllProjects();
        }

        #endregion

        private List<CmdArgumentJson> ReadCommandlineArgumentsFromProject(IVsHierarchy project)
        {
            var prjCmdArgs = new List<CmdArgumentJson>();
            Helper.ProjectArguments.AddAllArguments(project, prjCmdArgs);
            return prjCmdArgs;
        }

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
