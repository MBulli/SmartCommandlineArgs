using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using SmartCmdArgs.Services;
using SmartCmdArgs.Wrapper;
using System;

namespace SmartCmdArgs.Tests.Utils
{
    public static class Extensions
    {
        public static Mock<IVsHierarchyWrapper> Register(this Mock<IVsHierarchyWrapper> mock, Mock<IVisualStudioHelperService> vsHelperMock, Guid projectGuid)
        {
            mock.WithGuid(projectGuid);
            vsHelperMock.Setup(x => x.HierarchyForProjectGuid(projectGuid)).Returns(mock.Object);
            return mock;
        }

        public static Mock<IVsHierarchyWrapper> Register(this Mock<IVsHierarchyWrapper> mock, Mock<IVisualStudioHelperService> vsHelperMock)
        {
            return mock.Register(vsHelperMock, Guid.NewGuid());
        }

        public static Mock<IVsHierarchyWrapper> WithProjectDir(this Mock<IVsHierarchyWrapper> mock, string projectDir)
        {
            mock.Setup(x => x.GetProjectDir()).Returns(projectDir);
            return mock;
        }

        public static Mock<IVsHierarchyWrapper> WithGuid(this Mock<IVsHierarchyWrapper> mock, Guid projectGuid)
        {
            mock.Setup(x => x.GetGuid()).Returns(projectGuid);
            return mock;
        }

        public static Mock<IVsHierarchyWrapper> AsCpsProject(this Mock<IVsHierarchyWrapper> mock)
        {
            mock.Setup(x => x.IsCpsProject()).Returns(true);
            return mock;
        }

        public static Lazy<T> LazyObject<T>(this Mock<T> mock)
            where T : class
        {
            return new Lazy<T>(() => mock.Object);
        }
    }
}
