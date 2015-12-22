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
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindow))]
    [Guid(CmdArgsPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class CmdArgsPackage : Package
    {
        /// <summary>
        /// CmdArgsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "131b0c0a-5dd0-4680-b261-86ab5387b86e";
        public const string SolutionOptionKey = "SmartCommandlineArgsVA"; // Only letters are allowed
        private readonly string _VSConstants_VSStd97CmdID_GUID;

        private EnvDTE.DTE appObject;
        private EnvDTE.SolutionEvents solutionEvents;
        private EnvDTE.CommandEvents commandEvents;

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

            // cache guid value
            _VSConstants_VSStd97CmdID_GUID = typeof(VSConstants.VSStd97CmdID).GUID.ToString("B").ToUpper();

            // add option keys to store custom data in suo file
            this.AddOptionKey(SolutionOptionKey);
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Commands.Initialize(this);
            base.Initialize();

            this.appObject = (EnvDTE.DTE)GetService(typeof(SDTE));

            // see: https://support.microsoft.com/en-us/kb/555430
            this.solutionEvents = this.appObject.Events.SolutionEvents;
            this.commandEvents = this.appObject.Events.CommandEvents;

            this.solutionEvents.Opened += SolutionEvents_Opened;
            this.solutionEvents.AfterClosing += SolutionEvents_AfterClosing;
            this.solutionEvents.BeforeClosing += SolutionEvents_BeforeClosing;
            this.commandEvents.AfterExecute += CommandEvents_AfterExecute;

            this.ToolWindowViewModel.CommandlineArguments.DataCollection.CollectionChanged += ArgumentListChanged;
            this.ToolWindowViewModel.CommandlineArguments.DataCollection.ItemPropertyChanged += ArgumentListItemChanged;

            UpdateCurrentStartupProject();
            UpdateProjectConfiguration();
        }

        private void ArgumentListItemChanged(object sender, Helper.CollectionItemPropertyChangedEventArgs<ViewModel.CmdArgItem> e)
        {
            UpdateProjectConfiguration();
        }

        private void ArgumentListChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateProjectConfiguration();
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
                ToolWindowViewModel.CommandlineArguments.PopulateFromStream(stream);
                UpdateProjectConfiguration();
            }
        }

        protected override void OnSaveOptions(string key, Stream stream)
        {
            base.OnSaveOptions(key, stream);

            if (key == SolutionOptionKey)
            {
                ToolWindowViewModel.CommandlineArguments.StoreToStream(stream);
            }
        }


        #endregion

        private void UpdateProjectConfiguration()
        {
            EnvDTE.Project project;
            bool found = FindStartupProject(out project);

            if (found)
            {
                var activeEntries = ToolWindowViewModel.ActiveItemsForCurrentProject().Select(e => e.Command);
                string prjCmdArgs = string.Join(" ", activeEntries);

                foreach (EnvDTE.Configuration config in project.ConfigurationManager)
                {
                    if (config.Properties != null)
                    {
                        foreach (EnvDTE.Property prop in config.Properties)
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
        }

        #region VS Events

        private void SolutionEvents_Opened()
        {
            if (ToolWindowViewModel.CommandlineArguments.CmdLineItems.IsEmpty)
            {
                // Not working right now. Model changes aren't propagate to view
                //ReadCommandlineArgumentsFromAllProjects();
            }

            UpdateCurrentStartupProject();
            UpdateProjectConfiguration();
        }


        private void SolutionEvents_BeforeClosing()
        {
            ResetStartupProject();
        }

        private void SolutionEvents_AfterClosing()
        {

        }

        private void CommandEvents_AfterExecute(string Guid, int ID, object CustomIn, object CustomOut)
        {
            if (Guid == _VSConstants_VSStd97CmdID_GUID)
            {
                switch ((VSConstants.VSStd97CmdID)ID)
                {
                    case VSConstants.VSStd97CmdID.SetStartupProject:
                        UpdateCurrentStartupProject();
                        break;
                    case VSConstants.VSStd97CmdID.SolutionCfg: // this one is called frequently
                        UpdateCurrentStartupProject();
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        private void ReadCommandlineArgumentsFromAllProjects()
        {
            var result = new List<string>();
            var solution = this.appObject?.Solution;

            if (solution != null)
            {
                foreach (EnvDTE.Project project in solution.Projects)
                {
                    List<string> prjCmdArgs = new List<string>();
                    // Read properties for all configurations (e.g. Debug/Releas)
                    foreach (EnvDTE.Configuration config in project.ConfigurationManager)
                    {
                        if (config.Properties != null)
                        {
                            foreach (EnvDTE.Property prop in config.Properties)
                            {
                                // CommandArguments = C/C++ project
                                // StartArguments   = C#/VB project
                                if (prop.Name == "CommandArguments" || prop.Name == "StartArguments")
                                {
                                    string cmdarg = prop.Value as string;
                                    if (!string.IsNullOrEmpty(cmdarg))
                                    {
                                        result.Add(cmdarg);
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // Create entries for every distinct config property
                    foreach (var item in prjCmdArgs.Distinct())
                    {
                        ToolWindowViewModel.CommandlineArguments.AddNewItem(item, project.UniqueName, false);
                    }
                }
            }
        }

        private void UpdateCurrentStartupProject()
        {
            string prjName = StartupProjectUniqueName();

            // if startup project changed
            if (ToolWindowViewModel.StartupProject != prjName)
            {
                EnvDTE.Project project;
                if (FindProject(this.appObject?.Solution, prjName, out project))
                {
                    ToolWindowViewModel.UpdateStartupProject(project.UniqueName);
                }
            }
        }
        private void ResetStartupProject()
        {
            ToolWindowViewModel.UpdateStartupProject(null);
        }

        private string StartupProjectUniqueName()
        {
            var startupProjects = this.appObject?.Solution?.SolutionBuild?.StartupProjects as object[];
            return startupProjects?.FirstOrDefault() as string;
        }

        private bool FindStartupProject(out EnvDTE.Project startupProject)
        {
            string prjName = StartupProjectUniqueName();

            bool found = FindProject(this.appObject?.Solution, prjName, out startupProject);
            return found;
        }

        private bool FindProject(EnvDTE.Solution sln, string uniqueName, out EnvDTE.Project foundProject)
        {
            foundProject = null;

            if (sln == null || uniqueName == null)
                return false;

            foreach (EnvDTE.Project project in sln.Projects)
            {
                if (project.UniqueName == uniqueName)
                {
                    foundProject = project;
                    return true;
                }
                else if (project.Kind == EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
                {
                    // TODO search solution folders
                }
            }

            return false;
        }
    }
}
