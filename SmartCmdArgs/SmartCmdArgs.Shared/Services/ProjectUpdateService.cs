using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartCmdArgs.Services
{
    internal interface IProjectUpdateService
    {
        void UpdateCommandsForProject(IVsHierarchy project, SuoDataJson suoDataJson);
    }

    internal class ProjectUpdateService : IProjectUpdateService
    {
        private readonly CmdArgsPackage cmdArgsPackage;
        private readonly IOptionsSettingsService optionsSettings;
        private readonly IFileStorageService fileStorage;
        private readonly IProjectConfigService projectConfig;

        public ProjectUpdateService(IOptionsSettingsService optionsSettings, IFileStorageService fileStorage, IProjectConfigService projectConfig)
        {
            cmdArgsPackage = CmdArgsPackage.Instance;
            this.optionsSettings = optionsSettings;
            this.fileStorage = fileStorage;
            this.projectConfig = projectConfig;
        }

        public void UpdateCommandsForProject(IVsHierarchy project, SuoDataJson suoDataJson)
        {
            if (!cmdArgsPackage.IsEnabled)
                return;

            if (project == null)
                throw new ArgumentNullException(nameof(project));

            Logger.Info($"Update commands for project '{project?.GetName()}'. IsVcsSupportEnabled={optionsSettings.VcsSupportEnabled}. SolutionData.Count={suoDataJson?.ProjectArguments?.Count}.");

            var projectGuid = project.GetGuid();
            if (projectGuid == Guid.Empty)
            {
                Logger.Info("Skipping project because guid euqals empty.");
                return;
            }

            var solutionData = suoDataJson ?? new SuoDataJson();

            // joins data from solution and project
            //  => overrides solution commands for a project if a project json file exists
            //  => keeps all data from the suo file for projects without a json
            //  => if we have data in our ViewModel we use this instad of the suo file

            // get project json data
            ProjectDataJson projectData = null;
            if (optionsSettings.VcsSupportEnabled)
            {
                projectData = fileStorage.ReadDataForProject(project);
            }

            // data in project json overrides current data if it exists to respond to changes made by git to the file
            if (projectData != null)
            {
                Logger.Info($"Setting {projectData?.Items?.Count} commands for project '{project.GetName()}' from json-file.");

                var projectListViewModel = cmdArgsPackage.ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(projectGuid);

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
            else if (cmdArgsPackage.ToolWindowViewModel.TreeViewModel.Projects.ContainsKey(projectGuid))
            {
                return;
            }
            // if we dont have VCS enabld we try to read the suo file data
            else if (!optionsSettings.VcsSupportEnabled && solutionData.ProjectArguments.TryGetValue(projectGuid, out projectData))
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
            // if we don't have suo or json data we read cmd args from the project configs
            else
            {
                projectData = new ProjectDataJson();

                Logger.Info($"Gathering commands from configurations for project '{project.GetName()}'.");
                projectData.Items.AddRange(ReadCommandlineArgumentsFromProject(project));
            }

            // push projectData to the ViewModel
            cmdArgsPackage.ToolWindowViewModel.PopulateFromProjectData(project, projectData);

            Logger.Info($"Updated Commands for project '{project.GetName()}'.");
        }

        private List<CmdArgumentJson> ReadCommandlineArgumentsFromProject(IVsHierarchy project)
        {
            var prjCmdArgs = new List<CmdArgumentJson>();
            projectConfig.AddAllArguments(project, prjCmdArgs);
            return prjCmdArgs;
        }
    }
}
