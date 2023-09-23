using Moq;
using SmartCmdArgs.Services;
using SmartCmdArgs.ViewModel;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using SmartCmdArgs.Wrapper;

namespace SmartCmdArgs.Tests.Services
{
    public class ItemEvaluationServiceTests
    {
        private readonly Mock<IOptionsSettingsService> optionsSettingsMock;
        private readonly Mock<IVisualStudioHelperService> vsHelperMock;
        private readonly Mock<IItemPathService> itemPathMock;
        private readonly ItemEvaluationService itemEvaluationService;

        public ItemEvaluationServiceTests()
        {
            optionsSettingsMock = new Mock<IOptionsSettingsService>();
            vsHelperMock = new Mock<IVisualStudioHelperService>();
            itemPathMock = new Mock<IItemPathService>();

            itemEvaluationService = new ItemEvaluationService(optionsSettingsMock.Object, vsHelperMock.Object, itemPathMock.Object);
        }

        [Fact]
        public void TryParseEnvVar_ShouldReturnFalse_ForInvalidString()
        {
            // Act
            var result = itemEvaluationService.TryParseEnvVar("InvalidString", out var envVar);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryParseEnvVar_ShouldReturnTrue_AndSetCorrectValues_ForValidString()
        {
            // Arrange
            var str = "Name=Value";

            // Act
            var result = itemEvaluationService.TryParseEnvVar(str, out var envVar);

            // Assert
            Assert.True(result);
            Assert.Equal("Name", envVar.Name);
            Assert.Equal("Value", envVar.Value);
        }

        [Fact]
        public void EvaluateMacros_ShouldReturnOriginalArg_WhenMacroEvaluationIsDisabled()
        {
            // Arrange
            var arg = "$(PropertyName)";
            optionsSettingsMock.Setup(x => x.MacroEvaluationEnabled).Returns(false);

            // Act
            var result = itemEvaluationService.EvaluateMacros(arg, Mock.Of<IVsHierarchy>());

            // Assert
            Assert.Equal(arg, result);
        }

        [Theory]
        [InlineData("$(PropertyName)", "PropertyValue")]
        [InlineData("$(NonExistentProperty)", "$(NonExistentProperty)")]
        public void EvaluateMacros_ShouldReturnExpectedResult(string arg, string expected)
        {
            // Arrange
            var projectMock = new Mock<IVsHierarchy>();
            optionsSettingsMock.Setup(x => x.MacroEvaluationEnabled).Returns(true);
            vsHelperMock.Setup(x => x.GetMSBuildPropertyValueForActiveConfig(projectMock.Object, "PropertyName")).Returns("PropertyValue");

            // Act
            var result = itemEvaluationService.EvaluateMacros(arg, projectMock.Object);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\"Arg1 Arg2\" Arg3", new[] { "\"Arg1 Arg2\"", "Arg3" })]
        [InlineData("Arg1 Arg2 Arg3", new[] { "Arg1", "Arg2", "Arg3" })]
        [InlineData("\"Arg1\"", new[] { "\"Arg1\"" })]
        [InlineData("", new string[] { })]
        public void SplitArgument_ShouldReturnCorrectlySplitArguments(string argument, IEnumerable<string> expected)
        {
            // Act
            var result = itemEvaluationService.SplitArgument(argument);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractPathsFromItem_ShouldReturnEmpty_WhenProjectGuidIsEmpty()
        {
            // Arrange
            var cmdArgumentMock = new Mock<CmdArgument>(Guid.NewGuid(), ArgumentType.CmdArg, "Arg", true, false);
            cmdArgumentMock.SetupGet(x => x.ProjectGuid).Returns(Guid.Empty);

            // Act
            var result = itemEvaluationService.ExtractPathsFromItem(cmdArgumentMock.Object);

            // Assert
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(ArgumentType.CmdArg, "-i \"$(PropertyName)\"", new[] { "-i", "AbsolutePropertyValue" })]
        [InlineData(ArgumentType.EnvVar, "Name=\"$(PropertyName)\"", new[] { "AbsolutePropertyValue" })]
        [InlineData(ArgumentType.WorkDir, "\"$(PropertyName)\"", new[] { "AbsolutePropertyValue" })]
        public void ExtractPathsFromItem_ShouldEvaluateMacrosAndMakePathAbsolute_ForDifferentArgumentTypes(ArgumentType argType, string value, string[] expected)
        {
            // Arrange
            var projectGuid = Guid.NewGuid();
            var cmdArgumentMock = new Mock<CmdArgument>(argType, value, true, false);
            cmdArgumentMock.SetupGet(x => x.ProjectGuid).Returns(projectGuid);

            var hierarchyMock = new Mock<IVsHierarchy>();
            vsHelperMock.Setup(x => x.HierarchyForProjectGuid(projectGuid)).Returns(hierarchyMock.Object);
            vsHelperMock.Setup(x => x.GetMSBuildPropertyValueForActiveConfig(hierarchyMock.Object, "PropertyName")).Returns("PropertyValue");
            itemPathMock.Setup(x => x.MakePathAbsolute(It.IsAny<string>(), It.IsAny<IVsHierarchy>(), It.IsAny<string>())).Returns((string path, IVsHierarchy project, string buildConfig) => path);
            itemPathMock.Setup(x => x.MakePathAbsolute("PropertyValue", It.IsAny<IVsHierarchy>(), It.IsAny<string>())).Returns("AbsolutePropertyValue");
            optionsSettingsMock.Setup(x => x.MacroEvaluationEnabled).Returns(true);

            // Act
            var result = itemEvaluationService.ExtractPathsFromItem(cmdArgumentMock.Object);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractPathsFromItem_ShouldNotReturnPaths_WithInvalidPathCharacters()
        {
            // Arrange
            var item = new CmdArgument(Guid.NewGuid(), ArgumentType.CmdArg, "some<Arg");
            var projectGuid = Guid.NewGuid();
            var projectMock = new Mock<IVsHierarchy>();
            vsHelperMock.Setup(x => x.HierarchyForProjectGuid(projectGuid)).Returns(projectMock.Object);
            optionsSettingsMock.Setup(x => x.MacroEvaluationEnabled).Returns(false);
            itemPathMock.Setup(x => x.MakePathAbsolute(It.IsAny<string>(), It.IsAny<IVsHierarchyWrapper>(), It.IsAny<string>())).Returns((string path, IVsHierarchyWrapper project, string buildConfig) => path);

            // Act
            var result = itemEvaluationService.ExtractPathsFromItem(item);

            // Assert
            Assert.Empty(result);
        }

    }
}
