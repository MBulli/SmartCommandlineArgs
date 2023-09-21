using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartCmdArgs.Services
{
    public interface IItemAggregationService
    {
        IEnumerable<CmdArgument> GetAllComamndLineItemsForProject(IVsHierarchy project);
        string CreateCommandLineArgsForProject(IVsHierarchy project);
        IDictionary<string, string> GetEnvVarsForProject(IVsHierarchy project);
        string GetWorkDirForProject(IVsHierarchy project);
        string CreateCommandLineArgsForProject(Guid guid);
        IDictionary<string, string> GetEnvVarsForProject(Guid guid);
    }

    internal class ItemAggregationService : IItemAggregationService
    {
        private readonly IItemEvaluationService itemEvaluation;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly Lazy<ToolWindowViewModel> toolWindowViewModel;

        public ItemAggregationService(
            IItemEvaluationService itemEvaluation,
            IVisualStudioHelperService vsHelper,
            Lazy<ToolWindowViewModel> toolWindowViewModel)
        {
            this.itemEvaluation = itemEvaluation;
            this.vsHelper = vsHelper;
            this.toolWindowViewModel = toolWindowViewModel;
        }

        private TResult AggregateComamndLineItemsForProject<TResult>(IVsHierarchy project, Func<IEnumerable<CmdBase>, Func<CmdContainer, TResult>, CmdContainer, TResult> joinItems)
        {
            if (project == null)
                return default;

            var projectCmd = toolWindowViewModel.Value.TreeViewModel.Projects.GetValueOrDefault(project.GetGuid());
            if (projectCmd == null)
                return default;

            var projectObj = project.GetProject();

            string projConfig = projectObj?.ConfigurationManager?.ActiveConfiguration?.ConfigurationName;
            string projPlatform = projectObj?.ConfigurationManager?.ActiveConfiguration?.PlatformName;

            string activeLaunchProfile = null;
            if (project.IsCpsProject())
                activeLaunchProfile = CpsProjectSupport.GetActiveLaunchProfileName(projectObj);

            TResult JoinContainer(CmdContainer con)
            {
                IEnumerable<CmdBase> items = con.Items
                    .Where(x => x.IsChecked != false);

                if (projConfig != null)
                    items = items.Where(x => { var conf = x.UsedProjectConfig; return conf == null || conf == projConfig; });

                if (projPlatform != null)
                    items = items.Where(x => { var plat = x.UsedProjectPlatform; return plat == null || plat == projPlatform; });

                if (activeLaunchProfile != null)
                    items = items.Where(x => { var prof = x.UsedLaunchProfile; return prof == null || prof == activeLaunchProfile; });

                return joinItems(items, JoinContainer, con);
            }

            return JoinContainer(projectCmd);
        }

        public IEnumerable<CmdArgument> GetAllComamndLineItemsForProject(IVsHierarchy project)
        {
            IEnumerable<CmdArgument> joinItems(IEnumerable<CmdBase> items, Func<CmdContainer, IEnumerable<CmdArgument>> joinContainer, CmdContainer parentContainer)
            {
                foreach (var item in items)
                {
                    if (item is CmdContainer con)
                    {
                        foreach (var child in joinContainer(con))
                            yield return child;
                    }
                    else if (item is CmdArgument arg)
                    {
                        yield return arg;
                    }
                }
            }

            return AggregateComamndLineItemsForProject<IEnumerable<CmdArgument>>(project, joinItems);
        }

        public string CreateCommandLineArgsForProject(IVsHierarchy project)
        {
            return AggregateComamndLineItemsForProject<string>(project,
                (items, joinContainer, parentContainer) =>
                {
                    var strings = items
                        .Where(x => !(x is CmdArgument arg) || arg.ArgumentType == ArgumentType.CmdArg)
                        .Select(x => x is CmdContainer c ? joinContainer(c) : itemEvaluation.EvaluateMacros(x.Value, project))
                        .Where(x => !string.IsNullOrEmpty(x));

                    var joinedString = string.Join(parentContainer.Delimiter, strings);

                    return joinedString != string.Empty
                        ? parentContainer.Prefix + joinedString + parentContainer.Postfix
                        : string.Empty;
                });
        }

        public IDictionary<string, string> GetEnvVarsForProject(IVsHierarchy project)
        {
            var result = new Dictionary<string, string>();

            foreach (var item in GetAllComamndLineItemsForProject(project))
            {
                if (item.ArgumentType != ArgumentType.EnvVar) continue;

                if (itemEvaluation.TryParseEnvVar(item.Value, out EnvVar envVar))
                {
                    result[envVar.Name] = itemEvaluation.EvaluateMacros(envVar.Value, project);
                }
            }

            return result;
        }

        public string GetWorkDirForProject(IVsHierarchy project)
        {
            var result = "";

            foreach (var item in GetAllComamndLineItemsForProject(project))
            {
                if (item.ArgumentType != ArgumentType.WorkDir) continue;

                result = itemEvaluation.EvaluateMacros(item.Value, project);
            }

            return result;
        }

        public string CreateCommandLineArgsForProject(Guid guid)
        {
            return CreateCommandLineArgsForProject(vsHelper.HierarchyForProjectGuid(guid));
        }

        public IDictionary<string, string> GetEnvVarsForProject(Guid guid)
        {
            return GetEnvVarsForProject(vsHelper.HierarchyForProjectGuid(guid));
        }

    }
}
