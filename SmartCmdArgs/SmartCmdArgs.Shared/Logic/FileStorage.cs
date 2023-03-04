using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private FileSystemWatcher settingsFsWatcher;
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
            AttachFsWatcherToProject(project);
            AttachSolutionWatcher();
        }

        public void RemoveAllProjects()
        {
            DetachFsWatcherFromAllProjects();
            DetachSolutionWatcher();
        }

        public void RemoveProject(IVsHierarchy project)
        {
            DetachFsWatcherFromProject(project);
        }

        public void RenameProject(IVsHierarchy project, string oldProjectDir, string oldProjectName)
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

                        FireFileStorageChanged(project);
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

        private string GetSettingsPath()
        {
            string slnFilename = vsHelper.GetSolutionFilename();

            if (slnFilename == null)
                return null;

            return Path.ChangeExtension(slnFilename, "ArgsCfg.json");
        }

        public void SaveSettings()
        {
            string jsonFilename = GetSettingsPath();

            if (jsonFilename == null) return;

            using (settingsFsWatcher?.TemporarilyDisable())
            {
                if (cmdPackage.SaveSettingsToJson)
                {
                    string jsonStr = SettingsSerializer.Serialize(cmdPackage.ToolWindowViewModel.SettingsViewModel);

                    if (jsonStr == "{}")
                        File.Delete(jsonFilename);
                    else
                        File.WriteAllText(jsonFilename, jsonStr);
                }
                else
                {
                    File.Delete(jsonFilename);
                }
            }
        }

        public SettingsJson ReadSettings()
        {
            AttachSettingsWatcher();

            string jsonFilename = GetSettingsPath();

            if (jsonFilename != null && File.Exists(jsonFilename))
            {
                string jsonStr = File.ReadAllText(jsonFilename);

                return SettingsSerializer.Deserialize(jsonStr);
            }

            return null;
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

        public void DeleteAllUnusedArgFiles()
        {
            if (!cmdPackage.DeleteUnnecessaryFilesAutomatically)
                return;

            IEnumerable<string> fileNames;
            if (cmdPackage.IsUseSolutionDirEnabled)
                fileNames = vsHelper.GetSupportedProjects().Select(FullFilenameForProjectJsonFileFromProject);
            else
                fileNames = new[] { FullFilenameForSolutionJsonFile() };
            
            foreach (var fileName in fileNames)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception e)
                {
                    Logger.Warn($"Couldn't delete '{fileName}': {e}");
                }
            }
        }

        public void SaveProject(IVsHierarchy project)
        {
            if (cmdPackage.IsUseSolutionDirEnabled)
                SaveJsonForSolution();
            else
                SaveJsonForProject(project);
        }

        public void SaveAllProjects()
        {
            if (cmdPackage.IsUseSolutionDirEnabled)
                SaveJsonForSolution();
            else
                vsHelper.GetSupportedProjects().ForEach(SaveJsonForProject);
        }

        private void SaveJsonForSolution()
        {
            if (!cmdPackage.IsVcsSupportEnabled)
                return;

            string jsonFilename = FullFilenameForSolutionJsonFile();

            using (solutionFsWatcher?.TemporarilyDisable())
            {
                if (cmdPackage.ToolWindowViewModel.TreeViewModel.AllArguments.Any() || !cmdPackage.DeleteEmptyFilesAutomatically)
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

        private void SaveJsonForProject(IVsHierarchy project)
        {
            if (!cmdPackage.IsVcsSupportEnabled || project == null)
                return;

            var guid = project.GetGuid();
            var vm = cmdPackage.ToolWindowViewModel.TreeViewModel.Projects.GetValueOrDefault(guid);
            string filePath = FullFilenameForProjectJsonFileFromProject(project);
            FileSystemWatcher fsWatcher = projectFsWatchers.GetValueOrDefault(guid);

            if (vm != null && (vm.Items.Any() || !cmdPackage.DeleteEmptyFilesAutomatically))
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
            else if (File.Exists(filePath) && cmdPackage.DeleteEmptyFilesAutomatically)
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

        private string FullFilenameForSolutionJsonFile()
        {
            string slnFilename = vsHelper.GetSolutionFilename();
            return Path.ChangeExtension(slnFilename, "args.json");
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

        private void FireFileStorageChanged(FileStorageChanedType type)
        {
            FileStorageChanged?.Invoke(this, new FileStorageChangedEventArgs(type));
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

                if (slnFilename == null)
                    return;

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

        private void AttachSettingsWatcher()
        {
            if (settingsFsWatcher != null)
                return;

            var jsonFilename = GetSettingsPath();

            if (jsonFilename == null)
                return;

            try
            {
                settingsFsWatcher = new FileSystemWatcher();
                settingsFsWatcher.Path = Path.GetDirectoryName(jsonFilename);
                settingsFsWatcher.Filter = Path.GetFileName(jsonFilename);

                settingsFsWatcher.EnableRaisingEvents = true;

                settingsFsWatcher.Changed += (fsWatcher, args) => {
                    Logger.Info($"SystemFileWatcher file Change '{args.FullPath}'");
                    FireFileStorageChanged(FileStorageChanedType.Settings);
                };
                settingsFsWatcher.Created += (fsWatcher, args) => {
                    Logger.Info($"SystemFileWatcher file Created '{args.FullPath}'");
                    FireFileStorageChanged(FileStorageChanedType.Settings);
                };
                settingsFsWatcher.Renamed += (fsWatcher, args) =>
                {
                    Logger.Info($"FileWachter file Renamed '{args.FullPath}'. filename='{jsonFilename}'");
                    if (jsonFilename == args.FullPath)
                        FireFileStorageChanged(FileStorageChanedType.Settings);
                };

                Logger.Info($"Attached FileSystemWatcher to file '{jsonFilename}' for settings.");
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to attach FileSystemWatcher to file '{jsonFilename}' for settings with error '{e}'.");
            }
        }
    }

    enum FileStorageChanedType
    {
        Project,
        Solution,
        Settings,
    }

    class FileStorageChangedEventArgs : EventArgs
    {
        public readonly FileStorageChanedType Type;

        /// <summary>
        /// The project for that the event was triggered.
        /// Can be null if solution file triggered the event.
        /// </summary>
        public readonly IVsHierarchy Project;

        public bool IsSolutionWide => Type == FileStorageChanedType.Solution;

        public FileStorageChangedEventArgs(IVsHierarchy project)
        {
            Project = project;
            Type = project == null ? FileStorageChanedType.Solution : FileStorageChanedType.Project;
        }

        public FileStorageChangedEventArgs(FileStorageChanedType type)
        {
            Project = null;
            Type = type;
        }
    }
}
