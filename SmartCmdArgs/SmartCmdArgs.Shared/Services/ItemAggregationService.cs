using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using SmartCmdArgs.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartCmdArgs.Services
{
    public interface IItemAggregationService
    {
        IEnumerable<CmdParameter> GetAllComamndLineParamsForProject(IVsHierarchyWrapper project);
        string CreateCommandLineArgsForProject(IVsHierarchyWrapper project);
        IDictionary<string, string> GetEnvVarsForProject(IVsHierarchyWrapper project);
        string GetWorkDirForProject(IVsHierarchyWrapper project);
        string GetLaunchAppForProject(IVsHierarchyWrapper project);
        string CreateCommandLineArgsForProject(Guid guid);
        IDictionary<string, string> GetEnvVarsForProject(Guid guid);
    }

    internal class ItemAggregationService : IItemAggregationService
    {
        private readonly IItemEvaluationService itemEvaluation;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly TreeViewModel treeViewModel;
        private readonly ICpsProjectConfigService cpsProjectConfigService;

        public ItemAggregationService(
            IItemEvaluationService itemEvaluation,
            IVisualStudioHelperService vsHelper,
            TreeViewModel treeViewModel,
            ICpsProjectConfigService cpsProjectConfigService)
        {
            this.itemEvaluation = itemEvaluation;
            this.vsHelper = vsHelper;
            this.treeViewModel = treeViewModel;
            this.cpsProjectConfigService = cpsProjectConfigService;
        }

        private TResult AggregateComamndLineItemsForProject<TResult>(IVsHierarchyWrapper project, Func<IEnumerable<CmdBase>, Func<CmdContainer, TResult>, CmdContainer, TResult> joinItems)
        {
            if (project == null)
                return default;

            var projectCmd = treeViewModel.Projects.GetValueOrDefault(project.GetGuid());
            if (projectCmd == null)
                return default;

            var projectObj = project.GetProject();

            string projConfig = projectObj?.ConfigurationManager?.ActiveConfiguration?.ConfigurationName;
            string projPlatform = projectObj?.ConfigurationManager?.ActiveConfiguration?.PlatformName;

            string activeLaunchProfile = null;
            if (project.IsCpsProject())
                activeLaunchProfile = cpsProjectConfigService.GetActiveLaunchProfileName(projectObj);

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

        public IEnumerable<CmdParameter> GetAllComamndLineParamsForProject(IVsHierarchyWrapper project)
        {
            IEnumerable<CmdParameter> joinItems(IEnumerable<CmdBase> items, Func<CmdContainer, IEnumerable<CmdParameter>> joinContainer, CmdContainer parentContainer)
            {
                foreach (var item in items)
                {
                    if (item is CmdContainer con)
                    {
                        foreach (var child in joinContainer(con))
                            yield return child;
                    }
                    else if (item is CmdParameter param)
                    {
                        yield return param;
                    }
                }
            }

            return AggregateComamndLineItemsForProject<IEnumerable<CmdParameter>>(project, joinItems);
        }

        public string CreateCommandLineArgsForProject(IVsHierarchyWrapper project)
        {
            return AggregateComamndLineItemsForProject<string>(project,
                (items, joinContainer, parentContainer) =>
                {
                    var strings = items
                        .Where(x => !(x is CmdParameter param) || param.ParamType == CmdParamType.CmdArg)
                        .Select(x => x is CmdContainer c ? joinContainer(c) : itemEvaluation.EvaluateMacros(x.Value, project))
                        .Where(x => !string.IsNullOrEmpty(x));

                    var joinedString = string.Join(parentContainer.Delimiter, strings);

                    return joinedString != string.Empty
                        ? parentContainer.Prefix + joinedString + parentContainer.Postfix
                        : string.Empty;
                });
        }

        public IDictionary<string, string> GetEnvVarsForProject(IVsHierarchyWrapper project)
        {
            var result = new Dictionary<string, string>();

            foreach (var item in GetAllComamndLineParamsForProject(project))
            {
                if (item.ParamType != CmdParamType.EnvVar) continue;

                if (itemEvaluation.TryParseEnvVar(item.Value, out EnvVar envVar))
                {
                    result[envVar.Name] = itemEvaluation.EvaluateMacros(envVar.Value, project);
                }
            }

            return result;
        }

        public string GetWorkDirForProject(IVsHierarchyWrapper project)
        {
            var result = "";

            foreach (var item in GetAllComamndLineParamsForProject(project))
            {
                if (item.ParamType != CmdParamType.WorkDir) continue;

                result = itemEvaluation.EvaluateMacros(item.Value, project);
            }

            return result;
        }

        public string GetLaunchAppForProject(IVsHierarchyWrapper project)
        {
            var result = "";

            foreach (var item in GetAllComamndLineParamsForProject(project))
            {
                if (item.ParamType != CmdParamType.LaunchApp) continue;

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
