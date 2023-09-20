using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmartCmdArgs.Services
{
    internal interface IItemPathService
    {
        string MakePathAbsolute(string path, IVsHierarchy project, string buildConfig = null);
        string MakePathRelativeBasedOnSolutionDir(string path);
        string MakePathAbsoluteBasedOnSolutionDir(string path);
        string MakePathAbsoluteBasedOnProjectDir(string path, IVsHierarchy project);
        string MakePathAbsoluteBasedOnTargetDir(string path, IVsHierarchy project, string buildConfig);
    }

    internal class ItemPathService : IItemPathService
    {
        private readonly IOptionsSettingsService optionsSettings;
        private readonly IVisualStudioHelperService vsHelper;

        public ItemPathService(IOptionsSettingsService optionsSettings, IVisualStudioHelperService vsHelper)
        {
            this.optionsSettings = optionsSettings;
            this.vsHelper = vsHelper;
        }

        public string MakePathAbsolute(string path, IVsHierarchy project, string buildConfig = null)
        {
            switch (optionsSettings.RelativePathRoot)
            {
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
    }
}
