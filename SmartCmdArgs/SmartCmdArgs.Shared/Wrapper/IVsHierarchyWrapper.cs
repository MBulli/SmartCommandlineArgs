using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using SmartCmdArgs.Helper;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System;
using System.IO;
using EnvDTE;

namespace SmartCmdArgs.Wrapper
{
    public interface IVsHierarchyWrapper
    {
        IVsHierarchy Hierarchy { get; }

        List<Guid> GetAllTypeGuidsFromFile();
        string GetDisplayName();
        Guid GetGuid();
        Guid GetKind();
        string GetName();
        Project GetProject();
        string GetProjectDir();
        bool IsCpsProject();
        bool IsSharedAssetsProject();
        bool IsLoaded();
        bool TryGetIconMoniker(out ImageMoniker iconMoniker);
    }

    internal class VsHierarchyWrapper : IVsHierarchyWrapper
    {
        private readonly IVsHierarchy hierarchy;

        public IVsHierarchy Hierarchy => hierarchy;

        public VsHierarchyWrapper(IVsHierarchy hierarchy)
        {
            this.hierarchy = hierarchy;
        }

        private T GetProperty<T>(int propid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var error = hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, propid, out object prop);
            if (error != VSConstants.S_OK)
            {
                Logger.Warn($"Could not retrieve property with id={propid} from hierarchy");
            }
            else if (prop is T t)
            {
                return t;
            }

            return default(T);
        }

        private Guid GetGuidProperty(int propid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var error = hierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT, propid, out Guid prop);
            if (error != VSConstants.S_OK)
            {
                Logger.Warn($"Could not retrieve GUID with id={propid} from hierarchy");
                return Guid.Empty;
            }

            return prop;
        }

        public Project GetProject()
        {
            return GetProperty<Project>((int)__VSHPROPID.VSHPROPID_ExtObject);
        }

        /// <summary>
        /// Returns true if the hierachy object is a Common ProjectSystem object.
        /// see: https://github.com/dotnet/project-system
        /// see: https://github.com/Microsoft/VSProjectSystem/blob/master/doc/automation/detect_whether_a_project_is_a_CPS_project.md
        /// </summary>
        public bool IsCpsProject()
        {
            return hierarchy.IsCapabilityMatch("CPS");
        }

        public bool IsSharedAssetsProject()
        {
            return hierarchy.IsSharedAssetsProject();
        }

        public Guid GetGuid()
        {
            return GetGuidProperty((int)__VSHPROPID.VSHPROPID_ProjectIDGuid);
        }

        static readonly Regex TypeGuidParseRegex = new Regex(@"\<ProjectTypeGuids\>(?<Guids>.*?)<\/ProjectTypeGuids\>", RegexOptions.Singleline | RegexOptions.Compiled);

        public List<Guid> GetAllTypeGuidsFromFile()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var project = GetProject();
            var filePath = project.FullName;
            var fileContent = File.ReadAllText(filePath);
            var match = TypeGuidParseRegex.Match(fileContent);

            var list = new List<Guid>();
            if (match.Success)
            {
                foreach (var guidStr in match.Groups["Guids"].Value.Split(new[] { ';' }))
                {
                    if (Guid.TryParse(guidStr.Trim(), out Guid guid))
                    {
                        list.Add(guid);
                    }
                }
            }
            return list;
        }

        public Guid GetKind()
        {
            return GetGuidProperty((int)__VSHPROPID.VSHPROPID_TypeGuid);
        }

        public string GetProjectDir()
        {
            return GetProperty<string>((int)__VSHPROPID.VSHPROPID_ProjectDir);
        }

        public string GetName()
        {
            return GetProperty<string>((int)__VSHPROPID.VSHPROPID_Name);
        }

        public string GetDisplayName()
        {
            return GetProperty<string>((int)__VSHPROPID.VSHPROPID_Caption);
        }

        public bool TryGetIconMoniker(out ImageMoniker iconMoniker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var supportsIconMonikers = GetProperty<bool>((int)__VSHPROPID8.VSHPROPID_SupportsIconMonikers);
            if (supportsIconMonikers)
            {
                var iconMonikerImageList = GetProperty<IVsImageMonikerImageList>((int)__VSHPROPID8.VSHPROPID_IconMonikerImageList);

                if (iconMonikerImageList != null)
                {
                    ImageMoniker[] imageMonikers = new ImageMoniker[1];
                    iconMonikerImageList.GetImageMonikers(1, 1, imageMonikers);
                    iconMoniker = imageMonikers[0];
                    return true;
                }
            }
            iconMoniker = new ImageMoniker();
            return false;
        }

        public bool IsLoaded()
        {
            return null == GetProperty<object>((int)__VSHPROPID5.VSHPROPID_ProjectUnloadStatus);
        }
    }

    public static class IVsHierarchyExtension
    {
        private static ConditionalWeakTable<IVsHierarchy, IVsHierarchyWrapper> wrapperCache = new ConditionalWeakTable<IVsHierarchy, IVsHierarchyWrapper>();

        public static IVsHierarchyWrapper Wrap(this IVsHierarchy hierarchy)
        {
            if (hierarchy == null)
                return null;

            return wrapperCache.GetValue(hierarchy, x => new VsHierarchyWrapper(x));
        }
    }
}
