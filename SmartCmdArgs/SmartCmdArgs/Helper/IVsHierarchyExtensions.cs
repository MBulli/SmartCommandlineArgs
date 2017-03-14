using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace SmartCmdArgs.Helper
{
    static class IVsHierarchyExtensions
    {

        public static object GetExtObject(this IVsHierarchy hierarchy)
        {
            hierarchy.GetProperty(VSConstants.VSITEMID_ROOT,
                            (int)__VSHPROPID.VSHPROPID_ExtObject,
                            out object objProj);

            return objProj;
        }

        public static Guid GetGuid(this IVsHierarchy hierarchy)
        {
            hierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT,
                                        (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                                        out Guid guid);
            return guid;
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
