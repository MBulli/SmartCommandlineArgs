using SmartCmdArgs.Tests.Utils;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using SmartCmdArgs.Services;
using SmartCmdArgs.Wrapper;

namespace SmartCmdArgs.Tests.Services
{
    [Collection(MockedVS.Collection)]
    public class ItemPathServiceTests
    {
        private readonly Mock<IOptionsSettingsService> optionsSettingsMock;
        private readonly Mock<IVisualStudioHelperService> vsHelperMock;
        private readonly ItemPathService itemPathService;

        public ItemPathServiceTests(GlobalServiceProvider sp)
        {
            sp.Reset();

            optionsSettingsMock = new Mock<IOptionsSettingsService>();
            vsHelperMock = new Mock<IVisualStudioHelperService>();

            itemPathService = new ItemPathService(optionsSettingsMock.Object, vsHelperMock.Object);
        }

        [Fact]
        public void MakePathAbsoluteBasedOnSolutionDir_ShouldMakePathAbsolute()
        {
            // Arrange
            var path = "somepath";
            var solutionFileName = @"C:\SolutionDir\Solution.sln";
            vsHelperMock.Setup(x => x.GetSolutionFilename()).Returns(solutionFileName);

            var expectedPath = Path.Combine(Path.GetDirectoryName(solutionFileName), path);

            // Act
            var resultPath = itemPathService.MakePathAbsoluteBasedOnSolutionDir(path);

            // Assert
            Assert.Equal(expectedPath, resultPath);
        }

        [Fact]
        public void MakePathRelativeBasedOnSolutionDir_ShouldMakePathRelative()
        {
            // Arrange
            var path = @"C:\SolutionDir\somepath";
            var solutionFileName = @"C:\SolutionDir\Solution.sln";
            vsHelperMock.Setup(x => x.GetSolutionFilename()).Returns(solutionFileName);

            var expectedPath = @"somepath\";

            // Act
            var resultPath = itemPathService.MakePathRelativeBasedOnSolutionDir(path);

            // Assert
            Assert.Equal(expectedPath, resultPath);
        }

        [Theory]
        [InlineData(RelativePathRootOption.ProjectDirectory, @"C:\SolutionDir\ProjectDir", null)]
        [InlineData(RelativePathRootOption.BuildTargetDirectory, @"C:\SolutionDir\ProjectDir\bin", "Debug")]
        public async Task MakePathAbsolute_ShouldReturnExpectedPath(RelativePathRootOption rootOption, string expectedBaseDir, string buildConfig)
        {
            // Arrange
            var path = "somepath";
            var projectMock = new Mock<IVsHierarchyWrapper>();
            optionsSettingsMock.Setup(x => x.RelativePathRoot).Returns(rootOption);

            if (rootOption == RelativePathRootOption.ProjectDirectory)
                projectMock.WithProjectDir(expectedBaseDir);
            else
                vsHelperMock.Setup(x => x.GetMSBuildPropertyValue(It.IsAny<IVsHierarchyWrapper>(), "TargetDir", buildConfig)).Returns(expectedBaseDir);

            var expectedPath = expectedBaseDir == null ? path : $"{expectedBaseDir}\\{path}";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Act
            var resultPath = itemPathService.MakePathAbsolute(path, projectMock.Object, buildConfig);

            // Assert
            Assert.Equal(expectedPath, resultPath);
        }

        [Fact]
        public void MakePathAbsoluteBasedOnProjectDir_ShouldHandleNullProject()
        {
            // Arrange
            var path = "somepath";

            // Act
            var resultPath = itemPathService.MakePathAbsoluteBasedOnProjectDir(path, null);

            // Assert
            Assert.Null(resultPath);
        }

        [Fact]
        public void MakePathAbsoluteBasedOnTargetDir_ShouldHandleNullProjectAndBuildConfig()
        {
            // Arrange
            var path = "somepath";

            // Act
            var resultPath = itemPathService.MakePathAbsoluteBasedOnTargetDir(path, null, null);

            // Assert
            Assert.Null(resultPath);
        }

        [Fact]
        public void MakePathAbsolute_ShouldReturnNull_ForUnsupportedRelativePathRootOption()
        {
            // Arrange
            var path = "somepath";
            var projectMock = new Mock<IVsHierarchyWrapper>();
            optionsSettingsMock.Setup(x => x.RelativePathRoot).Returns((RelativePathRootOption)(-1)); // Unsupported option

            // Act
            var resultPath = itemPathService.MakePathAbsolute(path, projectMock.Object);

            // Assert
            Assert.Null(resultPath);
        }

        [Fact]
        public void MakePathAbsoluteBasedOnSolutionDir_ShouldHandleNullPath()
        {
            // Act
            var resultPath = itemPathService.MakePathAbsoluteBasedOnSolutionDir(null);

            // Assert
            Assert.Null(resultPath);
        }
    }
}
