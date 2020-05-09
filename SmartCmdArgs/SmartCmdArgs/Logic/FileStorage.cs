using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Logic
{
    /// <summary>
    /// This class handles all the file related functionality
    /// </summary>
    class FileStorage
    {
        private readonly CmdArgsPackage cmdPackage;
        private readonly VisualStudioHelper vsHelper;

        private Dictionary<Guid, FileSystemWatcher> projectFsWatchers = new Dictionary<Guid, FileSystemWatcher>();
        private FileSystemWatcher solutionFsWatcher;

        public event EventHandler<FileStorageChangedEventArgs> FileStorageChanged;

        public FileStorage(CmdArgsPackage cmdPackage, VisualStudioHelper vsHelper)
        {
            this.cmdPackage = cmdPackage;
            this.vsHelper = vsHelper;
        }

        public void AddProject(IVsHierarchy project)
        {
            if (!cmdPackage.IsUseSolutionDirEnabled)
            {
                AttachFsWatcherToProject(project);
            }
            else
            {
                AttachSolutionWatcher();
            }
        }


        public void RemoveAllProjects()
        {
            if (!cmdPackage.IsUseSolutionDirEnabled)
            {
                DetachFsWatcherFromAllProjects();
            }
            else
            {
                DetachSolutionWatcher();
            }
        }

        public void RemoveProject(IVsHierarchy project)
        {
            if (!cmdPackage.IsUseSolutionDirEnabled)
            {
                DetachFsWatcherFromProject(project);
            }
        }

        public void RenameProject(IVsHierarchy project, string oldProjectDir, string oldProjectName, Action hack)
        {
            if (cmdPackage.IsUseSolutionDirEnabled)
                return;

            var guid = project.GetGuid();
            if (projectFsWatchers.TryGetValue(guid, out FileSystemWatcher fsWatcher))
            {
                projectFsWatchers.Remove(guid);
                using (fsWatcher.TemporarilyDisable())
                {
                    var newFileName = FullFilenameForProjectJsonFileFromProject(project);
                    var oldFileName = FullFilenameForProjectJsonFileFromProjectPath(oldProjectDir, oldProjectName);

                    Logger.Info($"Renaming json-file '{oldFileName}' to new name '{newFileName}'");

                    if (File.Exists(newFileName))
                    {
                        File.Delete(oldFileName);

                        hack(); // TODO
                    }
                    else if (File.Exists(oldFileName))
                    {
                        File.Move(oldFileName, newFileName);
                    }
                    fsWatcher.Filter = Path.GetFileName(newFileName);
                }
                projectFsWatchers.Add(guid, fsWatcher);
            }
        }

        public ProjectDataJson ReadDataForProject(IVsHierarchy project)
        {
            ProjectDataJson result = null;

            if (!cmdPackage.IsUseSolutionDirEnabled)
            {
                string filePath = FullFilenameForProjectJsonFileFromProject(project);

                if (File.Exists(filePath))
                {
                    try
                    {
                        using (Stream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                        {
                            result = Logic.ProjectDataSerializer.Deserialize(fileStream);
                        }
                        Logger.Info($"Read {result?.Items?.Count} commands for project '{project.GetName()}' from json-file '{filePath}'.");
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Failed to read file '{filePath}' with error '{e}'.");
                        result = null;
                    }
                }
                else
                {
                    Logger.Info($"Json-file '{filePath}' doesn't exists.");
                }

                return result;
            }
            else
            {
                Guid projectGui = project.GetGuid();
                string slnFilename = vsHelper.GetSolutionFilename();
                string jsonFilename = Path.ChangeExtension(slnFilename, "args.json");

                if (File.Exists(jsonFilename))
                {
                    try
                    {
                        using (Stream fileStream = File.Open(jsonFilename, FileMode.Open, FileAccess.Read))
                        {
                            SolutionDataJson slnData = SolutionDataSerializer.Deserialize(fileStream);

                            result = slnData.ProjectArguments.FirstOrDefault(p => p.Id == projectGui);
                        }
                        Logger.Info($"Read {result?.Items?.Count} commands for project '{project.GetName()}' from json-file '{jsonFilename}'.");
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Failed to read file '{jsonFilename}' with error '{e}'.");
                        result = null;
                    }
                }
                else
                {
                    Logger.Info($"Json-file '{jsonFilename}' doesn't exists.");
                }

                return result;
            }
        }

        public void SaveProject(IVsHierarchy project)
        {
            if (!cmdPackage.IsUseSolutionDirEnabled)
            {
                SaveJsonForProject(project);
            }
            else
            {
                string slnFilename = vsHelper.GetSolutionFilename();
                string jsonFilename = Path.ChangeExtension(slnFilename, "args.json");

                using (solutionFsWatcher?.TemporarilyDisable())
                {
                    if (cmdPackage.ToolWindowViewModel.TreeViewModel.AllArguments.Any())
                    {
                        if (!vsHelper.CanEditFile(jsonFilename))
                        {
                            Logger.Error($"VS or the user did no let us edit our file :/ '{jsonFilename}'");
                        }
                        else
                        {
                            try
                            {
                                using (Stream fileStream = File.Open(jsonFilename, FileMode.Create, FileAccess.Write))
                                {
                                    SolutionDataSerializer.Serialize(cmdPackage.ToolWindowViewModel, fileStream);
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Warn($"Failed to write to file '{jsonFilename}' with error '{e}'.");
                            }
                        }
                    }
                    else
                    {
                        Logger.Info("Deleting solution json file because no project has command arguments but json file exists.");

                        try
                        {
                            File.Delete(jsonFilename);
                        }
                        catch (Exception e)
                        {
                            Logger.Warn($"Failed to delete file '{jsonFilename}' with error '{e}'.");
                        }
                    }
                }
            }
        }

        private void SaveJsonForProject(IVsHierarchy project)
        {
            if (!cmdPackage.IsVcsSupportEnabled || project == null)
                return;

            var guid = project.GetGuid();
            var vm = cmdPackage.ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(guid);
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
                        Logger.Error($"VS or the user did no let us edit our file :/");
                    }
                    else
                    {
                        try
                        {
                            using (Stream fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write))
                            {
                                ProjectDataSerializer.Serialize(vm, fileStream);
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

        private void FireFileStorageChanged(IVsHierarchy project)
        {
            FileStorageChanged?.Invoke(this, new FileStorageChangedEventArgs(project));
        }

        private void AttachFsWatcherToProject(IVsHierarchy project)
        {
            string unrealFilename = FullFilenameForProjectJsonFileFromProject(project);
            string realProjectJsonFileFullName = SymbolicLinkUtils.GetRealPath(unrealFilename);
            try
            {
                var projectJsonFileWatcher = new FileSystemWatcher();

                projectJsonFileWatcher.Path = Path.GetDirectoryName(realProjectJsonFileFullName);
                projectJsonFileWatcher.Filter = Path.GetFileName(realProjectJsonFileFullName);

                projectJsonFileWatcher.EnableRaisingEvents = true;
                projectFsWatchers.Add(project.GetGuid(), projectJsonFileWatcher);

                projectJsonFileWatcher.Changed += (fsWatcher, args) => {
                    Logger.Info($"SystemFileWatcher file Change '{args.FullPath}'");
                    FireFileStorageChanged(project);
                };
                projectJsonFileWatcher.Created += (fsWatcher, args) => {
                    Logger.Info($"SystemFileWatcher file Created '{args.FullPath}'");
                    FireFileStorageChanged(project);
                };
                projectJsonFileWatcher.Renamed += (fsWatcher, args) =>
                {
                    Logger.Info($"FileWachter file Renamed '{args.FullPath}'. realProjectJsonFileFullName='{realProjectJsonFileFullName}'");
                    if (realProjectJsonFileFullName == args.FullPath)
                        FireFileStorageChanged(project);
                };

                Logger.Info($"Attached FileSystemWatcher to file '{realProjectJsonFileFullName}' for project '{project.GetName()}'.");
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to attach FileSystemWatcher to file '{realProjectJsonFileFullName}' for project '{project.GetName()}' with error '{e}'.");
            }
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

        private void DetachFsWatcherFromAllProjects()
        {
            foreach (var projectFsWatcher in projectFsWatchers)
            {
                projectFsWatcher.Value.Dispose();
                Logger.Info($"Detached FileSystemWatcher for project '{projectFsWatcher.Key}'.");
            }
            projectFsWatchers.Clear();
        }


        private void AttachSolutionWatcher()
        {
            if (solutionFsWatcher == null)
            {
                string slnFilename = vsHelper.GetSolutionFilename();
                string jsonFilename = Path.ChangeExtension(slnFilename, "args.json");

                try
                {
                    solutionFsWatcher = new FileSystemWatcher();
                    solutionFsWatcher.Path = Path.GetDirectoryName(jsonFilename);
                    solutionFsWatcher.Filter = Path.GetFileName(jsonFilename);

                    solutionFsWatcher.EnableRaisingEvents = true;

                    solutionFsWatcher.Changed += (fsWatcher, args) => {
                        Logger.Info($"SystemFileWatcher file Change '{args.FullPath}'");
                        FireFileStorageChanged(null);
                    };
                    solutionFsWatcher.Created += (fsWatcher, args) => {
                        Logger.Info($"SystemFileWatcher file Created '{args.FullPath}'");
                        FireFileStorageChanged(null);
                    };
                    solutionFsWatcher.Renamed += (fsWatcher, args) =>
                    {
                        Logger.Info($"FileWachter file Renamed '{args.FullPath}'. filename='{jsonFilename}'");
                        if (jsonFilename == args.FullPath)
                            FireFileStorageChanged(null);
                    };

                    Logger.Info($"Attached FileSystemWatcher to file '{jsonFilename}' for solution.");
                }
                catch (Exception e)
                {
                    Logger.Warn($"Failed to attach FileSystemWatcher to file '{jsonFilename}' for solution with error '{e}'.");
                }
            }
        }
        private void DetachSolutionWatcher()
        {
            if (solutionFsWatcher != null)
            {
                solutionFsWatcher.Dispose();
                solutionFsWatcher = null;
            }
        }
    }

    class FileStorageChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The project for that the event was triggered.
        /// Can be null if solution file triggered the event.
        /// </summary>
        public readonly IVsHierarchy Project;

        public bool IsSolutionWide => Project != null;

        public FileStorageChangedEventArgs(IVsHierarchy project)
        {
            Project = project;
        }
    }
}
