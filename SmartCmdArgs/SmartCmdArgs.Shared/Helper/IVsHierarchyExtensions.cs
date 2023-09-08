using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SmartCmdArgs.Helper
{
    static class IVsHierarchyExtensions
    {

        public static T GetProperty<T>(this IVsHierarchy hierarchy, int propid)
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

        public static Project GetProject(this IVsHierarchy hierarchy)
        {
            return hierarchy.GetProperty<Project>((int)__VSHPROPID.VSHPROPID_ExtObject);
        }

        /// <summary>
        /// Returns true if the hierachy object is a Common ProjectSystem object.
        /// see: https://github.com/dotnet/project-system
        /// </summary>
        public static bool IsCpsProject(this IVsHierarchy hierarchy)
        {
            // see: https://github.com/Microsoft/VSProjectSystem/blob/master/doc/automation/detect_whether_a_project_is_a_CPS_project.md
            return hierarchy.IsCapabilityMatch("CPS");
        }

        /// <summary>
        /// Returns true if the hierachy object is a shared project (C#, C++ or VB).
        /// see: https://docs.microsoft.com/en-us/xamarin/cross-platform/app-fundamentals/shared-projects
        /// </summary>
        /// <param name="hierarchy"></param>
        /// <returns></returns>
        public static bool IsSharedAssetsProject(this IVsHierarchy hierarchy)
        {
            // see: https://docs.microsoft.com/en-us/visualstudio/extensibility/managing-universal-windows-projects
            return hierarchy.IsCapabilityMatch("SharedAssetsProject");
        }

        public static Guid GetGuid(this IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            hierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT,
                                        (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                                        out Guid guid);
            return guid;
        }

        static Regex TypeGuidParseRegex = new Regex(@"\<ProjectTypeGuids\>(?<Guids>.*?)<\/ProjectTypeGuids\>", RegexOptions.Singleline | RegexOptions.Compiled);

        public static List<Guid> GetAllTypeGuidsFromFile(this IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var project = hierarchy.GetProject();
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
        
        public static Guid GetKind(this IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            hierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT,
                (int)__VSHPROPID.VSHPROPID_TypeGuid,
                out Guid guid);
            return guid;
        }

        public static string GetProjectDir(this IVsHierarchy hierarchy)
        {
            return hierarchy.GetProperty<string>((int)__VSHPROPID.VSHPROPID_ProjectDir);
        }

        public static string GetName(this IVsHierarchy hierarchy)
        {
            return hierarchy.GetProperty<string>((int)__VSHPROPID.VSHPROPID_Name);
        }

        public static string GetDisplayName(this IVsHierarchy hierarchy)
        {
            return hierarchy.GetProperty<string>((int)__VSHPROPID.VSHPROPID_Caption);
        }

        public static bool TryGetIconMoniker(this IVsHierarchy hierarchy, out ImageMoniker iconMoniker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var supportsIconMonikers = hierarchy.GetProperty<bool>((int) __VSHPROPID8.VSHPROPID_SupportsIconMonikers);
            if (supportsIconMonikers)
            {
                var iconMonikerImageList = hierarchy.GetProperty<IVsImageMonikerImageList>((int)__VSHPROPID8.VSHPROPID_IconMonikerImageList);

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

        public static bool IsLoaded(this IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            hierarchy.GetProperty(VSConstants.VSITEMID_ROOT,
                                        (int)__VSHPROPID5.VSHPROPID_ProjectUnloadStatus,
                                        out object objLoaded);

            return objLoaded == null;
        }
    }
}
