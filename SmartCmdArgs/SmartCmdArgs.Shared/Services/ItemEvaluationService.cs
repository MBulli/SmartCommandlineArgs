using EnvDTE;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartCmdArgs.Services
{
    public interface IItemEvaluationService
    {
        bool TryParseEnvVar(string str, out EnvVar envVar);
        string EvaluateMacros(string arg, IVsHierarchy project);
        IEnumerable<string> SplitArgument(string argument);
        IEnumerable<string> ExtractPathsFromItem(CmdArgument item);
    }

    public struct EnvVar
    {
        public string Name;
        public string Value;
    }

    internal class ItemEvaluationService : IItemEvaluationService
    {
        private readonly IOptionsSettingsService optionsSettings;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly IItemPathService itemPath;

        private static readonly Regex msBuildPropertyRegex = new Regex(@"\$\((?<propertyName>(?:(?!\$\()[^)])*?)\)", RegexOptions.Compiled);
        private static readonly Regex SplitArgumentRegex = new Regex(@"(?:""(?:""""|\\""|[^""])*""?|[^\s""]+)+", RegexOptions.Compiled);

        public ItemEvaluationService(IOptionsSettingsService optionsSettings, IVisualStudioHelperService vsHelper, IItemPathService itemPath)
        {
            this.optionsSettings = optionsSettings;
            this.vsHelper = vsHelper;
            this.itemPath = itemPath;
        }

        public bool TryParseEnvVar(string str, out EnvVar envVar)
        {
            var parts = str.Split(new[] { '=' }, 2);

            if (parts.Length == 2)
            {
                envVar = new EnvVar { Name = parts[0], Value = parts[1] };
                return true;
            }

            envVar = new EnvVar();
            return false;
        }

        public string EvaluateMacros(string arg, IVsHierarchy project)
        {
            if (!optionsSettings.MacroEvaluationEnabled)
                return arg;

            if (project == null)
                return arg;

            return msBuildPropertyRegex.Replace(arg,
                match => vsHelper.GetMSBuildPropertyValueForActiveConfig(project, match.Groups["propertyName"].Value) ?? match.Value);
        }

        public IEnumerable<string> SplitArgument(string argument) => SplitArgumentRegex.Matches(argument).Cast<Match>().Select(x => x.Value);

        public IEnumerable<string> ExtractPathsFromItem(CmdArgument item)
        {
            var projectGuid = item.ProjectGuid;
            if (projectGuid == Guid.Empty)
                return Enumerable.Empty<string>();

            IVsHierarchy project = CmdArgsPackage.Instance.GetProjectForArg(item);

            var buildConfig = item.UsedProjectConfig;

            var parts = Enumerable.Empty<string>();

            switch (item.ArgumentType)
            {
                case ArgumentType.CmdArg:
                    parts = SplitArgument(EvaluateMacros(item.Value, project));
                    break;

                case ArgumentType.EnvVar:
                    var envVarParts = item.Value.Split(new[] { '=' }, 2);
                    if (envVarParts.Length == 2)
                        parts = new[] { EvaluateMacros(envVarParts[1], project) };
                    break;

                case ArgumentType.WorkDir:
                    parts = new[] { EvaluateMacros(item.Value, project) };
                    break;
            }

            return parts
                .Select(s => s.Trim('"'))
                .Where(s => s.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                .Select(s => itemPath.MakePathAbsolute(s, project, buildConfig))
                .Where(s => !string.IsNullOrEmpty(s));
        }
    }
}
