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
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.3.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow), Window = ToolWindow.ToolWindowGuidString)]
    [ProvideBindingPath]
    [Guid(CmdArgsPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideOptionPage(typeof(CmdArgsOptionPage), "Smart Commandline Arguments", "General", 1000, 1001, false)]
    [ProvideKeyBindingTable(ToolWindow.ToolWindowGuidString, 200)]
    public sealed class CmdArgsPackage : Package
    {
        /// <summary>
        /// CmdArgsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "131b0c0a-5dd0-4680-b261-86ab5387b86e";
        public const string ClipboardCmdItemFormat = "SmartCommandlineArgs_D11D715E-CBF3-43F2-A1C1-168FD5C48505";
        public const string SolutionOptionKey = "SmartCommandlineArgsVA"; // Only letters are allowed

        private readonly Regex msBuildPropertyRegex = new Regex(@"\$\((?<propertyName>(?:(?!\$\()[^)])*?)\)", RegexOptions.Compiled);

        private VisualStudioHelper vsHelper;
        public ViewModel.ToolWindowViewModel ToolWindowViewModel { get; } = new ViewModel.ToolWindowViewModel();

        private bool IsVcsSupportEnabled => GetDialogPage<CmdArgsOptionPage>().VcsSupport;
        private bool IsMacroEvaluationEnabled => GetDialogPage<CmdArgsOptionPage>().MacroEvaluation;

        private ToolWindowStateSolutionData toolWindowStateLoadedFromSolution;

        private Dictionary<string, FileSystemWatcher> projectFsWatchers = new Dictionary<string, FileSystemWatcher>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow"/> class.
        /// </summary>
        public CmdArgsPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.

            // add option keys to store custom data in suo file
            this.AddOptionKey(SolutionOptionKey);
        }

        #region Package Members
        internal Interface GetService<Service, Interface>()
        {
            return (Interface)base.GetService(typeof(Service));
        }

        internal Page GetDialogPage<Page>()
            where Page : class
        {
            return GetDialogPage(typeof(Page)) as Page;
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Commands.Initialize(this);
            base.Initialize();

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

            vsHelper.Initialize();

            // Extension window was opend while a solution is already open
            if (vsHelper.IsSolutionOpen)
            {
                Logger.Info("Package.Initialize called while solution was already open.");
                
                foreach (var projectName in vsHelper.GetSupportedProjectUniqueNames())
                {
                    UpdateCommandsForProject(projectName);
                    AttachFsWatcherToProject(projectName);
                }
                UpdateCurrentStartupProject();
            }
            
            ToolWindowViewModel.SelectedItemsChanged += OnSelectedItemsChanged;
        }

        private void OnSelectedItemsChanged(object sender, System.Collections.IList e)
        {
            vsHelper.UpdateShellCommandUI();
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
                toolWindowStateLoadedFromSolution = Logic.ToolWindowSolutionDataSerializer.Deserialize(stream);
            }
        }

        protected override void OnSaveOptions(string key, Stream stream)
        {
            base.OnSaveOptions(key, stream);
            if (key == SolutionOptionKey)
            {
                Logger.Info("Saving all commands.");

                if (IsVcsSupportEnabled)
                {
                    Logger.Info("VcsSupport is enabled.");

                    foreach (var projectName in ToolWindowViewModel.SolutionArguments.Keys)
                    {
                        var project = vsHelper.ProjectForProjectName(projectName);
                        SaveJsonForProject(project);
                    }
                }

                toolWindowStateLoadedFromSolution = ToolWindowSolutionDataSerializer.Serialize(ToolWindowViewModel, stream);
                Logger.Info("All Commands Saved.");
            }
        }

        private void SaveJsonForProject(Project project)
        {
            if (!IsVcsSupportEnabled || project == null)
                return;

            ListViewModel vm = ToolWindowViewModel.SolutionArguments[project.UniqueName];
            string filePath = FullFilenameForProjectJsonFileFromProject(project);
            FileSystemWatcher fsWatcher = projectFsWatchers.GetValueOrDefault(project.UniqueName);

            if (vm.DataCollection.Count != 0)
            {
                using (fsWatcher?.TemporarilyDisable())
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

        #endregion

        private void UpdateConfigurationForProject(Project project)
        {
            if (project == null) return;
            IEnumerable<string> enabledEntries;
            if (IsMacroEvaluationEnabled)
            {
                enabledEntries = ToolWindowViewModel.EnabledItemsForCurrentProject().Select(
                    e => msBuildPropertyRegex.Replace(e.Command,
                        match => vsHelper.GetMSBuildPropertyValue(project.UniqueName, match.Groups["propertyName"].Value) ?? match.Value));
            }
            else
            {
                enabledEntries = ToolWindowViewModel.EnabledItemsForCurrentProject().Select(e => e.Command);
            }
            string prjCmdArgs = string.Join(" ", enabledEntries);
            ProjectArguments.SetArguments(project, prjCmdArgs);
            Logger.Info($"Updated Configuration for Project: {project.UniqueName}");
        }

        private void AttachFsWatcherToProject(string projectName)
        {
            Project project = vsHelper.ProjectForProjectName(projectName);
            string realProjectJsonFileFullName = SymbolicLinkUtils.GetRealPath(FullFilenameForProjectJsonFileFromProject(project));
            try
            {
                var projectJsonFileWatcher = new FileSystemWatcher();

                projectJsonFileWatcher.Path = Path.GetDirectoryName(realProjectJsonFileFullName);
                projectJsonFileWatcher.Filter = Path.GetFileName(realProjectJsonFileFullName);

                projectJsonFileWatcher.EnableRaisingEvents = true;
                projectFsWatchers.Add(projectName, projectJsonFileWatcher);

                projectJsonFileWatcher.Changed += (fsWatcher, args) => { if (IsVcsSupportEnabled) UpdateCommandsForProjectOnDispatcher(projectName); };
                projectJsonFileWatcher.Created += (fsWatcher, args) => { if (IsVcsSupportEnabled) UpdateCommandsForProjectOnDispatcher(projectName); };
                projectJsonFileWatcher.Renamed += (fsWatcher, args) =>
                    { if (IsVcsSupportEnabled && realProjectJsonFileFullName == args.FullPath) UpdateCommandsForProjectOnDispatcher(projectName); };

                Logger.Info($"Attached FileSystemWatcher to file '{realProjectJsonFileFullName}' for project '{projectName}'.");
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to attach FileSystemWatcher to file '{realProjectJsonFileFullName}' for project '{projectName}' with error '{e}'.");
            }
        }

        private void DetachFsWatcherFromProject(string projectName)
        {
            FileSystemWatcher fsWatcher;
            if (projectFsWatchers.TryGetValue(projectName, out fsWatcher))
            {
                fsWatcher.Dispose();
                projectFsWatchers.Remove(projectName);
                Logger.Info($"Detached FileSystemWatcher for project '{projectName}'.");
            }
        }

        private void UpdateCommandsForProjectOnDispatcher(string projectName)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() =>
                {
                    UpdateCommandsForProject(projectName);
                }));
        }

        private void UpdateCommandsForProject(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
                throw new ArgumentNullException(nameof(projectName));

            Project project = vsHelper.ProjectForProjectName(projectName);
            
            Logger.Info($"Update commands for project '{projectName}'. IsVcsSupportEnabled={IsVcsSupportEnabled}. SolutionData.Count={toolWindowStateLoadedFromSolution?.Count}.");

            var solutionData = toolWindowStateLoadedFromSolution ?? new ToolWindowStateSolutionData();

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
                        Logger.Info($"Read {projectData?.DataCollection?.Count} commands for project '{projectName}' from json-file '{filePath}'.");
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
                Logger.Info($"Setting {projectData?.DataCollection?.Count} commands for project '{projectName}' from json-file.");

                ToolWindowStateProjectData curSolutionProjectData = solutionData.GetValueOrDefault(projectName);
                ListViewModel projectListViewModel = ToolWindowViewModel.SolutionArguments.GetValueOrDefault(projectName);

                // check if we have data in the suo file or the ViewModel
                if (curSolutionProjectData != null || projectListViewModel != null)
                {
                    // update enabled state of the project json data (source prio: ViewModel > suo file)
                    var dataCollectionFromProject = projectData?.DataCollection;
                    if (dataCollectionFromProject != null)
                    {
                        var dataCollectionFromSolution = curSolutionProjectData?.DataCollection;
                        var dataCollectionFromLVM = projectListViewModel?.DataCollection;
                        foreach (var dataFromProject in dataCollectionFromProject)
                        {
                            var dataFromVM = dataCollectionFromLVM?.FirstOrDefault(data => data.Id == dataFromProject.Id);

                            if (dataFromVM != null)
                                dataFromProject.Enabled = dataFromVM.Enabled;
                            else
                            {
                                var dataFromSolution =
                                    dataCollectionFromSolution?.Find(data => data.Id == dataFromProject.Id);

                                if (dataFromSolution != null)
                                    dataFromProject.Enabled = dataFromSolution.Enabled;
                            }
                        }
                    }
                    else
                    {
                        projectData = new ToolWindowStateProjectData();
                        Logger.Info($"DataCollection for project '{projectName}' is null.");
                    }
                }
            }
            // if we have data in the ViewModel we keep it
            else if (ToolWindowViewModel.SolutionArguments.ContainsKey(projectName))
            {
                return;
            }
            // we try to read the suo file data
            else if (!solutionData.TryGetValue(projectName, out projectData))
            {
                Logger.Info($"Gathering commands from configurations for project '{projectName}'.");
                // if we don't have suo file data we read cmd args from the project configs
                projectData = new ToolWindowStateProjectData();
                projectData.DataCollection.AddRange(
                    ReadCommandlineArgumentsFromProject(project)
                        .Select(cmdLineArg => new ToolWindowStateProjectData.ListEntryData {Command = cmdLineArg}));
            }
            else if (IsVcsSupportEnabled)
            {
                projectData = new ToolWindowStateProjectData();
                Logger.Info("Will clear all data because of missing json file and enabled VCS support.");
            }
            else
            {
                Logger.Info($"Will use commands from suo file for project '{projectName}'.");
            }

            // push projectData to the ViewModel
            ToolWindowViewModel.PopulateFromProjectData(projectName, projectData);

            Logger.Info($"Updated Commands for project '{projectName}'.");
        }

        #region VS Events
        private void VsHelper_SolutionOpend(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution opened.");
        }

        private void VsHelper_SolutionWillClose(object sender, EventArgs e)
        {
            Logger.Info("VS-Event: Solution will close.");
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

            if (ToolWindowViewModel.StartupProject != null)
            {
                var project = vsHelper.ProjectForProjectName(ToolWindowViewModel.StartupProject);
                UpdateConfigurationForProject(project);
                SaveJsonForProject(project);
            }
        }

        private void VsHelper_ProjectAdded(object sender, VisualStudioHelper.ProjectAfterOpenEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.UniqueName}' added. (IsLoadProcess={e.IsLoadProcess})");

            UpdateCommandsForProject(e.Project.UniqueName);
            AttachFsWatcherToProject(e.Project.UniqueName);
        }

        private void VsHelper_ProjectRemoved(object sender, VisualStudioHelper.ProjectBeforeCloseEventArgs e)
        {
            Logger.Info($"VS-Event: Project '{e.Project.UniqueName}' removed. (IsUnloadProcess={e.IsUnloadProcess})");

            if (ToolWindowViewModel.StartupProject == e.Project.UniqueName)
                ToolWindowViewModel.UpdateStartupProject(null);

            SaveJsonForProject(e.Project);

            if (!e.IsUnloadProcess)
                ToolWindowViewModel.SolutionArguments.Remove(e.Project.UniqueName);

            DetachFsWatcherFromProject(e.Project.UniqueName);
        }

        private void VsHelper_ProjectRenamed(object sender, VisualStudioHelper.ProjectAfterRenameEventArgs e)
        {
            Logger.Info("VS-Event: Project renamed.");

            FileSystemWatcher fsWatcher;
            if (projectFsWatchers.TryGetValue(e.Project.UniqueName, out fsWatcher))
            {
                using (fsWatcher.TemporarilyDisable())
                {
                    var newFileName = FullFilenameForProjectJsonFileFromProject(e.Project);
                    var oldFileName = FullFilenameForProjectJsonFileFromProjectPath(e.OldName);

                    Logger.Info($"Renaming json-file '{oldFileName}' to new name '{newFileName}'");

                    if (File.Exists(newFileName))
                    {
                        File.Delete(oldFileName);
                        UpdateCommandsForProject(e.Project.UniqueName);
                    }
                    else if (File.Exists(oldFileName))
                    {
                        File.Move(oldFileName, newFileName);
                    }
                    fsWatcher.Filter = Path.GetFileName(newFileName);
                }
            }
        }
        #endregion

        private IEnumerable<string> ReadCommandlineArgumentsFromProject(Project project)
        {
            List<string> prjCmdArgs = new List<string>();
            Helper.ProjectArguments.AddAllArguments(project, prjCmdArgs);
            return prjCmdArgs.Distinct();
        }

        private void UpdateCurrentStartupProject()
        {
            Project startupProject = vsHelper.GetStartupProject();

            if (ProjectArguments.IsSupportedProject(startupProject))
            {
                // update StartupProject
                ToolWindowViewModel.UpdateStartupProject(startupProject.UniqueName);
            }
            else
            {
                Logger.Info($"Unsupported startup project '{startupProject?.UniqueName}' of kind '{startupProject?.Kind}'");
                ToolWindowViewModel.UpdateStartupProject(null);
            }
        }

        private string FullFilenameForProjectJsonFileFromProject(EnvDTE.Project project)
        {
            var userFilename = vsHelper.GetMSBuildPropertyValue(project.UniqueName, "SmartCmdArgJsonFile");
            
            if (!string.IsNullOrEmpty(userFilename))
            {
                // It's recommended to use absolute paths for the json file in the first place...
                userFilename = Path.GetFullPath(userFilename); // ... but make it absolute in any case.
                
                Logger.Info($"'SmartCmdArgJsonFile' msbuild property present in project '{project.UniqueName}' will use json file '{userFilename}'.");
                return userFilename;
            }
            else
            {
                return FullFilenameForProjectJsonFileFromProjectPath(project.FullName);
            }
        }

        private string FullFilenameForProjectJsonFileFromProjectPath(string projectFile)
        {
            string filename = $"{Path.GetFileNameWithoutExtension(projectFile)}.args.json";
            return Path.Combine(Path.GetDirectoryName(projectFile), filename);
        }
    }
}
