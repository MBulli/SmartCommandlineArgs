using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace SmartCmdArgs.Helper
{
    static class IVsHierarchyExtensions
    {

        public static T GetProperty<T>(this IVsHierarchy hierarchy, int propid)
        {
            var error = hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, propid, out object prop);
            if (error != VSConstants.S_OK)
            {
                Logger.Warn($"Could not retrieve property with id={propid} from hierarchy");
            }
            else if (prop is T t)
                return t;
            return default(T);
        }

        public static Project GetProject(this IVsHierarchy hierarchy)
        {
            return hierarchy.GetProperty<Project>((int)__VSHPROPID.VSHPROPID_ExtObject);
        }

        public static Guid GetGuid(this IVsHierarchy hierarchy)
        {
            hierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT,
                                        (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                                        out Guid guid);
            return guid;
        }
        
        public static Guid GetKind(this IVsHierarchy hierarchy)
        {
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
            hierarchy.GetProperty(VSConstants.VSITEMID_ROOT,
                                        (int)__VSHPROPID5.VSHPROPID_ProjectUnloadStatus,
                                        out object objLoaded);

            return objLoaded == null;
        }
    }
}
