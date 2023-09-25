using Moq;
using SmartCmdArgs.Services;
using SmartCmdArgs.ViewModel;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using SmartCmdArgs.Wrapper;
using SmartCmdArgs.Tests.Utils;

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
            var result = itemEvaluationService.EvaluateMacros(arg, Mock.Of<IVsHierarchyWrapper>());

            // Assert
            Assert.Equal(arg, result);
        }

        [Theory]
        [InlineData("$(PropertyName)", "PropertyValue")]
        [InlineData("$(NonExistentProperty)", "$(NonExistentProperty)")]
        public void EvaluateMacros_ShouldReturnExpectedResult(string arg, string expected)
        {
            // Arrange
            var projectMock = new Mock<IVsHierarchyWrapper>();
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
            var cmdArgumentMock = new Mock<CmdParameter>(Guid.NewGuid(), CmdParamType.CmdArg, "Arg", true, false);
            cmdArgumentMock.SetupGet(x => x.ProjectGuid).Returns(Guid.Empty);

            // Act
            var result = itemEvaluationService.ExtractPathsFromParameter(cmdArgumentMock.Object);

            // Assert
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(CmdParamType.CmdArg, "-i \"$(PropertyName)\"", new[] { "-i", "AbsolutePropertyValue" })]
        [InlineData(CmdParamType.EnvVar, "Name=\"$(PropertyName)\"", new[] { "AbsolutePropertyValue" })]
        [InlineData(CmdParamType.WorkDir, "\"$(PropertyName)\"", new[] { "AbsolutePropertyValue" })]
        public void ExtractPathsFromItem_ShouldEvaluateMacrosAndMakePathAbsolute_ForDifferentArgumentTypes(CmdParamType argType, string value, string[] expected)
        {
            // Arrange
            var projectGuid = Guid.NewGuid();
            var cmdParameterMock = new Mock<CmdParameter>(argType, value, true, false);
            cmdParameterMock.SetupGet(x => x.ProjectGuid).Returns(projectGuid);

            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperMock, projectGuid).Object;
            vsHelperMock.Setup(x => x.GetMSBuildPropertyValueForActiveConfig(project, "PropertyName")).Returns("PropertyValue");
            itemPathMock.Setup(x => x.MakePathAbsolute(It.IsAny<string>(), It.IsAny<IVsHierarchyWrapper>(), It.IsAny<string>())).Returns((string path, IVsHierarchyWrapper prj, string buildConfig) => path);
            itemPathMock.Setup(x => x.MakePathAbsolute("PropertyValue", It.IsAny<IVsHierarchyWrapper>(), It.IsAny<string>())).Returns("AbsolutePropertyValue");
            optionsSettingsMock.Setup(x => x.MacroEvaluationEnabled).Returns(true);

            // Act
            var result = itemEvaluationService.ExtractPathsFromParameter(cmdParameterMock.Object);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractPathsFromItem_ShouldNotReturnPaths_WithInvalidPathCharacters()
        {
            // Arrange
            var item = new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "some<Arg");
            var projectGuid = Guid.NewGuid();
            var projectMock = new Mock<IVsHierarchyWrapper>();
            vsHelperMock.Setup(x => x.HierarchyForProjectGuid(projectGuid)).Returns(projectMock.Object);
            optionsSettingsMock.Setup(x => x.MacroEvaluationEnabled).Returns(false);
            itemPathMock.Setup(x => x.MakePathAbsolute(It.IsAny<string>(), It.IsAny<IVsHierarchyWrapper>(), It.IsAny<string>())).Returns((string path, IVsHierarchyWrapper project, string buildConfig) => path);

            // Act
            var result = itemEvaluationService.ExtractPathsFromParameter(item);

            // Assert
            Assert.Empty(result);
        }

    }
}
