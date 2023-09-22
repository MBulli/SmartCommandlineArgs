using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;

namespace SmartCmdArgs.Tests.Utils
{

    public static class Extensions
    {
        public static Mock<IVsHierarchy> WithProperty(this Mock<IVsHierarchy> mock, int propId, object value)
        {
            mock.Setup(x => x.GetProperty(VSConstants.VSITEMID_ROOT, propId, out value)).Returns(VSConstants.S_OK);

            return mock;
        }

        public static Mock<IVsHierarchy> WithProjectDir(this Mock<IVsHierarchy> mock, string projectDir)
            => mock.WithProperty((int)__VSHPROPID.VSHPROPID_ProjectDir, projectDir);
    }
}
