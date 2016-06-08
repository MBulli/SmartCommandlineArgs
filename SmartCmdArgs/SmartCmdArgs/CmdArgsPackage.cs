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
using System.Windows;
using System.Windows.Threading;
using EnvDTE;
using SmartCmdArgs.Logic;

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
    [InstalledProductRegistration("#110", "#112", "1.1", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow))]
    [ProvideBindingPath]
    [Guid(CmdArgsPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideOptionPage(typeof(CmdArgsOptionPage), "Smart Commandline Arguments", "General", 1000, 1001, false)]
    public sealed class CmdArgsPackage : Package
    {
        /// <summary>
        /// CmdArgsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "131b0c0a-5dd0-4680-b261-86ab5387b86e";
        public const string ClipboardCmdItemFormat = "SmartCommandlineArgs_D11D715E-CBF3-43F2-A1C1-168FD5C48505";
        public const string SolutionOptionKey = "SmartCommandlineArgsVA"; // Only letters are allowed

        private VisualStudioHelper vsHelper;
        public ViewModel.ToolWindowViewModel ToolWindowViewModel { get; } = new ViewModel.ToolWindowViewModel();

        private bool IsSvcSupportEnabled { get { return GetDialogPage<CmdArgsOptionPage>().SvcSupport; } }

        private ToolWindowStateSolutionData toolWindowStateLoadedFromSolution;

        private Dictionary<Project, FileSystemWatcher> projectFsWatcherDictionary = new Dictionary<Project, FileSystemWatcher>();

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
            vsHelper.SolutionOpend += VsHelper_SolutionOpend;
            vsHelper.SolutionWillClose += VsHelper_SolutionWillClose;
            vsHelper.SolutionClosed += VsHelper_SolutionClosed;
            vsHelper.StartupProjectChanged += VsHelper_StartupProjectChanged;
            vsHelper.StartupProjectConfigurationChanged += VsHelper_StartupProjectConfigurationChanged;

            vsHelper.ProjectAdded += VsHelper_ProjectAdded;
            vsHelper.ProjectRemoved += VsHelper_ProjectRemoved;
            vsHelper.ProjectRenamed += VsHelper_ProjectRenamed;

            // Extension window was opend while a solution is already open
            if (vsHelper.IsSolutionOpen)
            {
                VsHelper_SolutionOpend(this, EventArgs.Empty);
            }

            ToolWindowViewModel.CommandLineChanged += OnCommandLineChanged;
            ToolWindowViewModel.SelectedItemsChanged += OnSelectedItemsChanged;

            UpdateCurrentStartupProject();
            UpdateProjectConfiguration();
        }

        private void OnCommandLineChanged(object sender, EventArgs e)
        {
            UpdateProjectConfiguration();
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
                if (IsSvcSupportEnabled)
                {
                    foreach (EnvDTE.Project project in vsHelper.FindAllProjects())
                    {
                        ViewModel.ListViewModel vm = null;
                        if (ToolWindowViewModel.SolutionArguments.TryGetValue(project.UniqueName, out vm))
                        {
                            string filePath = FullFilenameForProjectJsonFile(project);
                            
                            using (Stream fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write))
                            {
                                Logic.ToolWindowProjectDataSerializer.Serialize(vm, fileStream);
                            }
                        }
                    }
                }

                Logic.ToolWindowSolutionDataSerializer.Serialize(ToolWindowViewModel, stream);
            }
        }

        #endregion

        private void UpdateProjectConfiguration()
        {
            EnvDTE.Project project;
            bool found = vsHelper.FindStartupProject(out project);

            if (found)
            {
                var activeEntries = ToolWindowViewModel.ActiveItemsForCurrentProject().Select(e => e.Command);
                string prjCmdArgs = string.Join(" ", activeEntries);
                Helper.ProjectArguments.SetArguments(project, prjCmdArgs);
            }
        }

        private void AttachFsWatcherToProject(Project project)
        {
            var projectJsonFileWatcher = new FileSystemWatcher();
            projectJsonFileWatcher.Changed += (fsWatcher, args) => UpdateProjectCommands(project);
            projectJsonFileWatcher.Created += (fsWatcher, args) => UpdateProjectCommands(project);

            string projectJsonFileFullName = FullFilenameForProjectJsonFile(project);
            projectJsonFileWatcher.Path = Path.GetDirectoryName(projectJsonFileFullName);
            projectJsonFileWatcher.Filter = Path.GetFileName(projectJsonFileFullName);
            projectJsonFileWatcher.EnableRaisingEvents = true;
            projectFsWatcherDictionary.Add(project, projectJsonFileWatcher);
        }

        private void DetachFsWatcherFromProject(Project project)
        {
            FileSystemWatcher fsWatcher;
            if (projectFsWatcherDictionary.TryGetValue(project, out fsWatcher))
            {
                fsWatcher.Dispose();
                projectFsWatcherDictionary.Remove(project);
            }
        }

        private void DetachFsWatcherFromAllProjects()
        {
            foreach (var pair in projectFsWatcherDictionary)
            {
                pair.Value.Dispose();
            }
            projectFsWatcherDictionary.Clear();
        }

        private void UpdateProjectCommands(Project project)
        {
            if (!IsSvcSupportEnabled) return;

            var solutionData = toolWindowStateLoadedFromSolution ?? new ToolWindowStateSolutionData();
            var currentData = ToolWindowViewModel.GetListViewModel(project.UniqueName).DataCollection;

            if (string.IsNullOrEmpty(project.FileName))
                return;

            string filePath = FullFilenameForProjectJsonFile(project);

            if (!File.Exists(filePath))
                return;

            using (Stream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                ToolWindowStateProjectData projectData = Logic.ToolWindowProjectDataSerializer.Deserialize(fileStream);

                if (projectData == null)
                    return;

                // joins data from solution and project
                //  => overrides solution commands for a project if a project json file exists
                //  => keeps all data from the solution for projects without a json
                ToolWindowStateProjectData curSolutionProjectData;
                if (solutionData.TryGetValue(project.UniqueName, out curSolutionProjectData))
                {
                    var dataCollectionFromSolution = curSolutionProjectData?.DataCollection;
                    var dataCollectionFromProject = projectData?.DataCollection;
                    if (dataCollectionFromSolution != null && dataCollectionFromProject != null)
                    {
                        foreach (var dataFromProject in dataCollectionFromProject)
                        {
                            var dataFromVM = currentData.FirstOrDefault(data => data.Id == dataFromProject.Id);

                            if (dataFromVM != null)
                                dataFromProject.Enabled = dataFromVM.Enabled;
                            else
                            {

                                var dataFromSolution = dataCollectionFromSolution.Find(data => data.Id == dataFromProject.Id);

                                if (dataFromSolution != null)
                                    dataFromProject.Enabled = dataFromSolution.Enabled;
                            }
                        }
                    }
                }
                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        ToolWindowViewModel.PopulateFromProjectData(project.UniqueName, projectData);
                        UpdateProjectConfiguration();
                    }));
            }
        }

        #region VS Events
        private void VsHelper_SolutionOpend(object sender, EventArgs e)
        {
            vsHelper.Initialize();

            if (IsSvcSupportEnabled)
            {
                var solutionData = toolWindowStateLoadedFromSolution ?? new ToolWindowStateSolutionData();

                foreach (EnvDTE.Project project in vsHelper.FindAllProjects())
                {
                    if (string.IsNullOrEmpty(project.FileName))
                        continue;

                    string filePath = FullFilenameForProjectJsonFile(project);

                    if (!File.Exists(filePath))
                        continue;

                    using (Stream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                    {
                        ToolWindowStateProjectData projectData = Logic.ToolWindowProjectDataSerializer.Deserialize(fileStream);

                        if (projectData == null)
                            continue;

                        // joins data from solution and project
                        //  => overrides solution commands for a project if a project json file exists
                        //  => keeps all data from the solution for projects without a json
                        ToolWindowStateProjectData curSolutionProjectData;
                        if(solutionData.TryGetValue(project.UniqueName, out curSolutionProjectData))
                        {
                            var dataCollectionFromSolution = curSolutionProjectData?.DataCollection;
                            var dataCollectionFromProject = projectData?.DataCollection;
                            if (dataCollectionFromSolution != null && dataCollectionFromProject != null)
                            {
                                foreach (var dataFromProject in dataCollectionFromProject)
                                {
                                    var dataFromSolution = dataCollectionFromSolution.Find(data => data.Id == dataFromProject.Id);

                                    if (dataFromSolution == null)
                                        continue;
                                    
                                    dataFromProject.Enabled = dataFromSolution.Enabled;
                                }
                            }
                        }
                        solutionData[project.UniqueName] = projectData;
                    }
                }
                ToolWindowViewModel.PopulateFromSolutionData(solutionData);
            }
            else if (toolWindowStateLoadedFromSolution != null)
            {
                ToolWindowViewModel.PopulateFromSolutionData(toolWindowStateLoadedFromSolution);
            }
            else
            {
                var allCommands = ReadCommandlineArgumentsFromAllProjects();
                if (allCommands != null)
                    ToolWindowViewModel.PopulateFromDictinary(allCommands);
            }

            UpdateCurrentStartupProject();
            UpdateProjectConfiguration();
        }

        private void VsHelper_SolutionWillClose(object sender, EventArgs e)
        {
            DetachFsWatcherFromAllProjects();
        }

        private void VsHelper_SolutionClosed(object sender, EventArgs e)
        {
            ToolWindowViewModel.Reset();
            toolWindowStateLoadedFromSolution = null;

            vsHelper.Deinitalize();
        }

        private void VsHelper_StartupProjectChanged(object sender, EventArgs e)
        {
            UpdateCurrentStartupProject();
        }

        private void VsHelper_StartupProjectConfigurationChanged(object sender, EventArgs e)
        {
            UpdateProjectConfiguration();
        }

        private void VsHelper_ProjectAdded(object sender, Project project)
        {
            AttachFsWatcherToProject(project);
        }

        private void VsHelper_ProjectRemoved(object sender, Project project)
        {
            DetachFsWatcherFromProject(project);
        }

        private void VsHelper_ProjectRenamed(object sender, VisualStudioHelper.ProjectRenamedEventArgs e)
        {
            FileSystemWatcher fsWatcher;
            if (projectFsWatcherDictionary.TryGetValue(e.project, out fsWatcher))
            {
                fsWatcher.EnableRaisingEvents = false;
                var newFileName = FullFilenameForProjectJsonFile(e.project);
                var oldFileName = FullFilenameForProjectJsonFile(e.oldName);
                if (File.Exists(oldFileName) && !File.Exists(newFileName))
                    File.Move(FullFilenameForProjectJsonFile(e.oldName), newFileName);
                fsWatcher.Filter = Path.GetFileName(newFileName);
                fsWatcher.EnableRaisingEvents = true;
            }
        }
        #endregion

        private Dictionary<string, IList<string>> ReadCommandlineArgumentsFromAllProjects()
        {
            var dict = new Dictionary<string, IList<string>>();
            var allProjects = vsHelper.FindAllProjects();

            foreach (EnvDTE.Project project in allProjects)
            {
                List<string> prjCmdArgs = new List<string>();
                Helper.ProjectArguments.AddAllArguments(project, prjCmdArgs);
                dict.Add(project.UniqueName, prjCmdArgs.Distinct().ToList());
            }

            return dict;
        }

        private void UpdateCurrentStartupProject()
        {
            string prjName = vsHelper.StartupProjectUniqueName();

            // if startup project changed
            if (ToolWindowViewModel.StartupProject != prjName)
            {
                ToolWindowViewModel.UpdateStartupProject(prjName);
            }
        }


        private string FullFilenameForProjectJsonFile(EnvDTE.Project project)
        {
            return FullFilenameForProjectJsonFile(project.FileName);
        }

        private string FullFilenameForProjectJsonFile(string projectFile)
        {
            string filename = string.Format("{0}.args.json", Path.GetFileNameWithoutExtension(projectFile));
            return Path.Combine(Path.GetDirectoryName(projectFile), filename);
        }
    }
}
