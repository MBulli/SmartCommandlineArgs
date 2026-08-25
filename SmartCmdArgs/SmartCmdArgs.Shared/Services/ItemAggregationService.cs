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

        public ItemAggregationService(
            IItemEvaluationService itemEvaluation,
            IVisualStudioHelperService vsHelper,
            TreeViewModel treeViewModel)
        {
            this.itemEvaluation = itemEvaluation;
            this.vsHelper = vsHelper;
            this.treeViewModel = treeViewModel;
        }

        private TResult AggregateComamndLineItemsForProject<TResult>(IVsHierarchyWrapper project, Func<IEnumerable<CmdBase>, Func<CmdContainer, TResult>, CmdContainer, TResult> joinItems, bool includeUnchecked = false)
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
                activeLaunchProfile = CpsProjectSupport.GetActiveLaunchProfileName(projectObj);

            TResult JoinContainer(CmdContainer con)
            {
                IEnumerable<CmdBase> items = con.Items;

                if (!includeUnchecked)
                    items = items.Where(x => x.IsChecked != false);

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
            => GetAllComamndLineParamsForProject(project, includeUnchecked: false);

        private IEnumerable<CmdParameter> GetAllComamndLineParamsForProject(IVsHierarchyWrapper project, bool includeUnchecked)
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

            return AggregateComamndLineItemsForProject<IEnumerable<CmdParameter>>(project, joinItems, includeUnchecked)
                ?? Enumerable.Empty<CmdParameter>();
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
            // If the project has no environment variable items at all, this extension does not manage the
            // environment for it. Returning null (instead of an empty dictionary) prevents the caller from
            // overwriting the environment which is defined by the project itself.
            var items = GetAllComamndLineParamsForProject(project, includeUnchecked: true)
                .Where(x => x.ParamType == CmdParamType.EnvVar)
                .ToList();

            if (items.Count == 0)
                return null;

            var result = new Dictionary<string, string>();

            foreach (var item in items.Where(x => x.IsChecked != false))
            {
                if (itemEvaluation.TryParseEnvVar(item.Value, out EnvVar envVar))
                {
                    result[envVar.Name] = itemEvaluation.EvaluateMacros(envVar.Value, project);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the value of the last active parameter of the given type or
        /// <c>null</c> if the project has no parameter of that type at all.
        /// <para>
        /// Returning <c>null</c> is important: it tells the caller that this extension does not manage
        /// this setting for the project, so the project's own value must be left untouched. Returning an
        /// empty string instead would overwrite (and thereby erase) settings like the debugger command or
        /// the working directory that are defined by the project itself.
        /// </para>
        /// </summary>
        private string GetSingleParamValueForProject(IVsHierarchyWrapper project, CmdParamType paramType)
        {
            // Unchecked items are included here on purpose. If the project has items of this type but all
            // of them are unchecked, the user explicitly disabled them and we return an empty string to
            // clear the setting. Only if there is no item of this type at all we return null.
            var items = GetAllComamndLineParamsForProject(project, includeUnchecked: true)
                .Where(x => x.ParamType == paramType)
                .ToList();

            if (items.Count == 0)
                return null;

            var result = "";

            foreach (var item in items.Where(x => x.IsChecked != false))
            {
                result = itemEvaluation.EvaluateMacros(item.Value, project);
            }

            return result;
        }

        public string GetWorkDirForProject(IVsHierarchyWrapper project)
        {
            return GetSingleParamValueForProject(project, CmdParamType.WorkDir);
        }

        public string GetLaunchAppForProject(IVsHierarchyWrapper project)
        {
            return GetSingleParamValueForProject(project, CmdParamType.LaunchApp);
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
