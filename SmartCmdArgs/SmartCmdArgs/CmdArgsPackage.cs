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
    [InstalledProductRegistration("#110", "#112", "2.0.7", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow), Window = ToolWindow.ToolWindowGuidString)]
    [ProvideOptionPage(typeof(CmdArgsOptionPage), "Smart Commandline Arguments", "General", 1000, 1001, false)]   
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
        public ToolWindowViewModel ToolWindowViewModel { get; }

        private bool IsVcsSupportEnabled => GetDialogPage<CmdArgsOptionPage>().VcsSupport;
        private bool IsMacroEvaluationEnabled => GetDialogPage<CmdArgsOptionPage>().MacroEvaluation;
        private bool IsUseMonospaceFontEnabled => GetDialogPage<CmdArgsOptionPage>().UseMonospaceFont;

        // We store the commandline arguments also in the suo file.
        // This is handled in the OnLoad/SaveOptions methods.
        // As the parser needs a initialized instance of vsHelper,
        // the json string from the suo is saved in this variable and
        // processed later.
        private string toolWindowStateFromSolutionJsonStr;
        private ToolWindowStateSolutionData toolWindowStateLoadedFromSolution;

        private Dictionary<Guid, FileSystemWatcher> projectFsWatchers = new Dictionary<Guid, FileSystemWatcher>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow"/> class.
        /// </summary>
        public CmdArgsPackage()
        {   
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.

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

            await vsHelper.InitializeAsync();

            // Switch to main thread
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            GetDialogPage<CmdArgsOptionPage>().VcsSupportChanged += OptionPage_VcsSupportChanged;
            GetDialogPage<CmdArgsOptionPage>().UseMonospaceFontChanged += OptionPage_UseMonospaceFontChanged;

            // Extension window was opend while a solution is already open
            if (vsHelper.IsSolutionOpen)
            {
                Logger.Info("Package.Initialize called while solution was already open.");

                InitializeForSolution();
            }

            ToolWindowViewModel.TreeViewModel.ItemSelectionChanged += OnItemSelectionChanged;
            ToolWindowViewModel.TreeViewModel.TreeContentChanged += OnTreeContentChanged;

            ToolWindowViewModel.UseMonospaceFont = IsUseMonospaceFontEnabled;

            await base.InitializeAsync(cancellationToken, progress);
        }

        private void OnTreeContentChanged(object sender, TreeViewModel.TreeChangedEventArgs e)
        {
            if (IsVcsSupportEnabled)
            {
                Logger.Info($"Tree content changed and VCS support is enabled. Saving all project commands to json file for project '{e.AffectedProject.Id}'.");

                var projectGuid = e.AffectedProject.Id;

                try
                {
                    var project = vsHelper.HierarchyForProjectGuid(projectGuid);
                    SaveJsonForProject(project);
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
                return new ToolWindow(ToolWindowViewModel);
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
                toolWindowStateLoadedFromSolution = ToolWindowSolutionDataSerializer.Serialize(ToolWindowViewModel, stream);
                Logger.Info("All Commands saved to suo file.");
            }
        }

        private void SaveJsonForProject(IVsHierarchy project)
        {
            if (!IsVcsSupportEnabled || project == null)
                return;

            var guid = project.GetGuid();
            var vm = ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(guid);
            string filePath = FullFilenameForProjectJsonFileFromProject(project);
            FileSystemWatcher fsWatcher = projectFsWatchers.GetValueOrDefault(guid);

            if (vm != null && vm.Items.Any())
            {
                using (fsWatcher?.TemporarilyDisable())
                {
                    // Tell VS that we're about to change this file
                    // This matters if the user has TFVC with server workpace (see #57)

                    if (!vsHelper.CanEditFile(filePath))
                    {
                        Logger.Error($"VS or the user did no let us editing our file :/");
                    }
                    else
                    {
                        try
                        {
                            using (Stream fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write))
                            {
                                Logic.ToolWindowProjectDataSerializer.Serialize(vm, fileStream);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Warn($"Failed to write to file '{filePath}' with error '{e}'.");
                        }
                    }
                }
            }
            else if (File.Exists(filePath))
            {
                Logger.Info("Deleting json file because command list is empty but json-file exists.");

                try
                {
                    File.Delete(filePath);
                }
                catch (Exception e)
                {
                    Logger.Warn($"Failed to delete file '{filePath}' with error '{e}'.");
                }
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

        private string CreateCommandLineArgsForProject(IVsHierarchy project)
        {
            if (project == null)
                return null;

            IEnumerable<CmdArgument> checkedArgs = ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(project.GetGuid())?.CheckedArguments;
            if (checkedArgs == null)
                return null;

            string projConfig = project.GetProject()?.ConfigurationManager?.ActiveConfiguration?.ConfigurationName;
            if (projConfig != null)
                checkedArgs = checkedArgs.Where(x => { var conf = x.UsedProjectConfig; return conf == null || conf == projConfig; });

            IEnumerable<string> enabledEntries;
            if (IsMacroEvaluationEnabled)
            {
                enabledEntries = checkedArgs.Select(
                    e => msBuildPropertyRegex.Replace(e.Value,
                        match => vsHelper.GetMSBuildPropertyValue(project, match.Groups["propertyName"].Value) ?? match.Value));
            }
            else
            {
                enabledEntries = checkedArgs.Select(e => e.Value);
            }
            return string.Join(" ", enabledEntries);
        }

        public string CreateCommandLineArgsForProject(Guid guid)
        {
            return CreateCommandLineArgsForProject(vsHelper.HierarchyForProjectGuid(guid));
        }

        private void AttachFsWatcherToProject(IVsHierarchy project)
        {
            string realProjectJsonFileFullName = SymbolicLinkUtils.GetRealPath(FullFilenameForProjectJsonFileFromProject(project));
            try
            {
                var projectJsonFileWatcher = new FileSystemWatcher();

                projectJsonFileWatcher.Path = Path.GetDirectoryName(realProjectJsonFileFullName);
                projectJsonFileWatcher.Filter = Path.GetFileName(realProjectJsonFileFullName);

                projectJsonFileWatcher.EnableRaisingEvents = true;
                projectFsWatchers.Add(project.GetGuid(), projectJsonFileWatcher);

                projectJsonFileWatcher.Changed += (fsWatcher, args) => {
                    Logger.Info($"SystemFileWatcher file Change '{args.FullPath}'");
                    UpdateCommandsForProjectOnDispatcher(project, onlyIfVcsSupportEnabled: true);
                };
                projectJsonFileWatcher.Created += (fsWatcher, args) => {
                    Logger.Info($"SystemFileWatcher file Created '{args.FullPath}'");
                    UpdateCommandsForProjectOnDispatcher(project, onlyIfVcsSupportEnabled: true);
                };
                projectJsonFileWatcher.Renamed += (fsWatcher, args) =>
                {
                    Logger.Info($"FileWachter file Renamed '{args.FullPath}'. realProjectJsonFileFullName='{realProjectJsonFileFullName}'");
                    if (realProjectJsonFileFullName == args.FullPath)
                        UpdateCommandsForProjectOnDispatcher(project, onlyIfVcsSupportEnabled: true);
                };

                Logger.Info($"Attached FileSystemWatcher to file '{realProjectJsonFileFullName}' for project '{project.GetName()}'.");
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to attach FileSystemWatcher to file '{realProjectJsonFileFullName}' for project '{project.GetName()}' with error '{e}'.");
            }
        }

        private void DetachFsWatcherFromAllProjects()
        {
            foreach (var projectFsWatcher in projectFsWatchers)
            {
                projectFsWatcher.Value.Dispose();
                Logger.Info($"Detached FileSystemWatcher for project '{projectFsWatcher.Key}'.");
            }
            projectFsWatchers.Clear();
        }

        private void DetachFsWatcherFromProject(IVsHierarchy project)
        {
            var guid = project.GetGuid();
            if (projectFsWatchers.TryGetValue(guid, out FileSystemWatcher fsWatcher))
            {
                fsWatcher.Dispose();
                projectFsWatchers.Remove(guid);
                Logger.Info($"Detached FileSystemWatcher for project '{project.GetName()}'.");
            }
        }

        private void UpdateCommandsForProjectOnDispatcher(IVsHierarchy project, bool onlyIfVcsSupportEnabled)
        {
            Logger.Info($"Dispatching update commands function call for project '{project.GetDisplayName()}'");

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

                if (onlyIfVcsSupportEnabled && !IsVcsSupportEnabled)
                    return;

                Logger.Info($"Dispatched update commands function call for project '{project.GetDisplayName()}'");

                if (project.GetGuid() == Guid.Empty)
                {
                    Logger.Info($"Race condition might occurred while dispatching update commands function call. Project is already unloaded.");
                }

                UpdateCommandsForProject(project);
            });
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

            var solutionData = toolWindowStateLoadedFromSolution ?? new ToolWindowStateSolutionData();

            ToolWindowViewModel.TreeViewModel.ShowAllProjects = solutionData.ShowAllProjects;

            // joins data from solution and project
            //  => overrides solution commands for a project if a project json file exists
            //  => keeps all data from the suo file for projects without a json
            //  => if we have data in our ViewModel we use this instad of the suo file

            // get project json data
            ToolWindowStateProjectData projectData = null;
            if (IsVcsSupportEnabled)
            {
                string filePath = FullFilenameForProjectJsonFileFromProject(project);

                if (File.Exists(filePath))
                {
                    try
                    {
                        using (Stream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                        {
                            projectData = Logic.ToolWindowProjectDataSerializer.Deserialize(fileStream);
                        }
                        Logger.Info($"Read {projectData?.Items?.Count} commands for project '{project.GetName()}' from json-file '{filePath}'.");
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Failed to read file '{filePath}' with error '{e}'.");
                        projectData = null;
                    }
                }
                else
                {
                    Logger.Info($"Json-file '{filePath}' doesn't exists.");
                }
            }

            // project json overrides if it exists
            if (projectData != null)
            {
                Logger.Info($"Setting {projectData?.Items?.Count} commands for project '{project.GetName()}' from json-file.");
                
                var projectListViewModel = ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(projectGuid);
                
                // update enabled state of the project json data (source prio: ViewModel > suo file)
                if (projectData.Items != null)
                {
                    var argumentDataFromProject = projectData.AllArguments;
                    var argumentDataFromLVM = projectListViewModel?.AllArguments.ToDictionary(arg => arg.Id, arg => arg);
                    foreach (var dataFromProject in argumentDataFromProject)
                    {
                        if (argumentDataFromLVM != null && argumentDataFromLVM.TryGetValue(dataFromProject.Id, out CmdArgument argFromVM))
                            dataFromProject.Enabled = argFromVM.IsChecked;
                        else
                            dataFromProject.Enabled = solutionData.CheckedArguments.Contains(dataFromProject.Id);
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
                    
                    if (projectListViewModel != null)
                        projectData.Expanded = projectListViewModel.IsExpanded;
                    else
                        projectData.Expanded = solutionData.ExpandedContainer.Contains(projectData.Id);
                }
                else
                {
                    projectData = new ToolWindowStateProjectData();
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
                projectData = new ToolWindowStateProjectData();
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

                projectData.Expanded = solutionData.ExpandedContainer.Contains(projectData.Id);
            }
            else
            {
                Logger.Info($"Gathering commands from configurations for project '{project.GetName()}'.");
                // if we don't have suo file data we read cmd args from the project configs
                projectData = new ToolWindowStateProjectData();
                projectData.Items.AddRange(
                    ReadCommandlineArgumentsFromProject(project)
                        .Select(cmdLineArg => new ListEntryData { Command = cmdLineArg }));
            }

            // push projectData to the ViewModel
            ToolWindowViewModel.PopulateFromProjectData(project, projectData);

            Logger.Info($"Updated Commands for project '{project.GetName()}'.");
        }

        private void InitializeForSolution()
        {
            toolWindowStateLoadedFromSolution = Logic.ToolWindowSolutionDataSerializer.Deserialize(toolWindowStateFromSolutionJsonStr, vsHelper);

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                UpdateCommandsForProject(project);
                AttachFsWatcherToProject(project);
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

            DetachFsWatcherFromAllProjects();
        }

        private void VsHelper_SolutionClosed(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution closed.");

            ToolWindowViewModel.Reset();
            toolWindowStateLoadedFromSolution = null;
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
                SaveJsonForProject(project);
            }
        }

        private void VsHelper_ProjectAdded(object sender, VisualStudioHelper.ProjectAfterOpenEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' added. (IsLoadProcess={e.IsLoadProcess}, IsSolutionOpenProcess={e.IsSolutionOpenProcess})");

            if (e.IsSolutionOpenProcess)
                return;

            UpdateCommandsForProject(e.Project);
            AttachFsWatcherToProject(e.Project);
        }

        private void VsHelper_ProjectRemoved(object sender, VisualStudioHelper.ProjectBeforeCloseEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.GetName()}' removed. (IsUnloadProcess={e.IsUnloadProcess}, IsSolutionCloseProcess={e.IsSolutionCloseProcess})");

            if (e.IsSolutionCloseProcess)
                return;

            SaveJsonForProject(e.Project);

            ToolWindowViewModel.TreeViewModel.Projects.Remove(e.Project.GetGuid());

            DetachFsWatcherFromProject(e.Project);
        }

        private void VsHelper_ProjectRenamed(object sender, VisualStudioHelper.ProjectAfterRenameEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.OldProjectName}' renamed to '{e.Project.GetName()}'.");

            var guid = e.Project.GetGuid();
            if (projectFsWatchers.TryGetValue(guid, out FileSystemWatcher fsWatcher))
            {
                projectFsWatchers.Remove(guid);
                using (fsWatcher.TemporarilyDisable())
                {
                    var newFileName = FullFilenameForProjectJsonFileFromProject(e.Project);
                    var oldFileName = FullFilenameForProjectJsonFileFromProjectPath(e.OldProjectDir, e.OldProjectName);

                    Logger.Info($"Renaming json-file '{oldFileName}' to new name '{newFileName}'");

                    if (File.Exists(newFileName))
                    {
                        File.Delete(oldFileName);
                        UpdateCommandsForProject(e.Project);
                    }
                    else if (File.Exists(oldFileName))
                    {
                        File.Move(oldFileName, newFileName);
                    }
                    fsWatcher.Filter = Path.GetFileName(newFileName);
                }
                projectFsWatchers.Add(guid, fsWatcher);
            }
            ToolWindowViewModel.RenameProject(e.Project);
        }
        #endregion

        #region OptionPage Events
        private void OptionPage_VcsSupportChanged(object sender, bool enabled)
        {
            if (!enabled)
                return;

            foreach (var projectName in vsHelper.GetSupportedProjects())
            {
                UpdateCommandsForProject(projectName);
            }
        }

        private void OptionPage_UseMonospaceFontChanged(object sender, bool enabled)
        {
            ToolWindowViewModel.UseMonospaceFont = enabled;
        }
        #endregion

        private IEnumerable<string> ReadCommandlineArgumentsFromProject(IVsHierarchy project)
        {
            List<string> prjCmdArgs = new List<string>();
            Helper.ProjectArguments.AddAllArguments(project, prjCmdArgs);
            return prjCmdArgs.Where(arg => !string.IsNullOrWhiteSpace(arg)).Distinct();
        }

        private void UpdateCurrentStartupProject()
        {
            var startupProjectGuids = new HashSet<Guid>(vsHelper.StartupProjectUniqueNames()
                .Select(vsHelper.HierarchyForProjectName).Select(hierarchy => hierarchy.GetGuid()));

            ToolWindowViewModel.TreeViewModel.Projects.ForEach(p => p.Value.IsStartupProject = startupProjectGuids.Contains(p.Key));
            ToolWindowViewModel.TreeViewModel.UpdateTree();
        }

        private string FullFilenameForProjectJsonFileFromProject(IVsHierarchy project)
        {
            var userFilename = vsHelper.GetMSBuildPropertyValue(project, "SmartCmdArgJsonFile");
            
            if (!string.IsNullOrEmpty(userFilename))
            {
                // It's recommended to use absolute paths for the json file in the first place...
                userFilename = Path.GetFullPath(userFilename); // ... but make it absolute in any case.
                
                Logger.Info($"'SmartCmdArgJsonFile' msbuild property present in project '{project.GetName()}' will use json file '{userFilename}'.");
                return userFilename;
            }
            else
            {
                return FullFilenameForProjectJsonFileFromProjectPath(project.GetProjectDir(), project.GetName());
            }
        }

        private string FullFilenameForProjectJsonFileFromProjectPath(string projectDir, string projectName)
        {
            string filename = $"{projectName}.args.json";
            return Path.Combine(projectDir, filename);
        }
    }
}
