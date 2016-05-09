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
            vsHelper.StartupProjectChanged += VsHelper_StartupProjectChanged;
            vsHelper.StartupProjectConfigurationChanged += VsHelper_StartupProjectConfigurationChanged;
           
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
                var args = Logic.ToolWindowSolutionDataSerializer.DeserializeFromSolution(stream);
                ToolWindowViewModel.PopulateFromSolutionData(args);
                UpdateProjectConfiguration();
            }
        }

        protected override void OnSaveOptions(string key, Stream stream)
        {
            base.OnSaveOptions(key, stream);

            if (key == SolutionOptionKey)
            {
                Logic.ToolWindowSolutionDataSerializer.SerializeToSolution(ToolWindowViewModel, stream);
            }
        }

        #endregion

        private void UpdateProjectConfiguration()
        {
            EnvDTE.Project project;
            bool found = vsHelper.FindStartupProject(out project);

            if (found)
            {
                EnvDTE.Properties properties = project.ConfigurationManager?.ActiveConfiguration?.Properties;

                if (properties != null)
                {
                    var activeEntries = ToolWindowViewModel.ActiveItemsForCurrentProject().Select(e => e.Command);
                    string prjCmdArgs = string.Join(" ", activeEntries);

                    foreach (EnvDTE.Property prop in properties)
                    {
                        // CommandArguments = C/C++ project
                        // StartArguments   = C#/VB project
                        if (prop.Name == "CommandArguments" || prop.Name == "StartArguments")
                        {
                            prop.Value = prjCmdArgs;
                            break;
                        }
                    }
                }
            }
        }

        #region VS Events
        private void VsHelper_SolutionOpend(object sender, EventArgs e)
        {
            if (!ToolWindowViewModel.Initialized)
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
            ToolWindowViewModel.Reset();
        }

        private void VsHelper_StartupProjectChanged(object sender, EventArgs e)
        {
            UpdateCurrentStartupProject();
        }

        private void VsHelper_StartupProjectConfigurationChanged(object sender, EventArgs e)
        {
            UpdateProjectConfiguration();
        }
        #endregion

        private Dictionary<string, IList<string>> ReadCommandlineArgumentsFromAllProjects()
        {
            var dict = new Dictionary<string, IList<string>>();
            var solution = vsHelper.Solution;

            if (solution == null)
                return null;

            foreach (EnvDTE.Project project in solution.Projects)
            {
                List<string> prjCmdArgs = new List<string>();
                // Read properties for all configurations (e.g. Debug/Releas)
                foreach (EnvDTE.Configuration config in project.ConfigurationManager)
                {
                    if (config.Properties == null)
                        continue;

                    foreach (EnvDTE.Property prop in config.Properties)
                    {
                        // CommandArguments = C/C++ project
                        // StartArguments   = C#/VB project
                        if (prop.Name != "CommandArguments" && prop.Name != "StartArguments")
                            continue;

                        string cmdarg = prop.Value as string;
                        if (!string.IsNullOrEmpty(cmdarg))
                        {
                            prjCmdArgs.Add(cmdarg);
                        }
                        break;
                    }
                }
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
    }
}
