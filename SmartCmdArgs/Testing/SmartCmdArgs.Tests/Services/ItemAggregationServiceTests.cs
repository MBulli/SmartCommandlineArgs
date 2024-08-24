using Moq;
using System;
using System.Collections.Generic;
using SmartCmdArgs.ViewModel;
using SmartCmdArgs.Services;
using Xunit;
using Microsoft.VisualStudio.Sdk.TestFramework;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.Tests.Utils;
using SmartCmdArgs.Wrapper;
using System.Linq;

namespace SmartCmdArgs.Tests.Services
{
    [Collection(MockedVS.Collection)]
    public class ItemAggregationServiceTests
    {
        private readonly Mock<IItemEvaluationService> itemEvaluationServiceMock;
        private readonly Mock<IVisualStudioHelperService> vsHelperServiceMock;
        private readonly Mock<IToolWindowHistory> toolWindowHistoryMock;
        private readonly Mock<ICpsProjectConfigService> cpsProjectConfigService;
        private readonly TreeViewModel treeViewModel;
        private readonly ItemAggregationService itemAggregationService;

        public ItemAggregationServiceTests(GlobalServiceProvider sp)
        {
            sp.Reset();

            itemEvaluationServiceMock = new Mock<IItemEvaluationService>();
            vsHelperServiceMock = new Mock<IVisualStudioHelperService>();
            toolWindowHistoryMock = new Mock<IToolWindowHistory>();
            cpsProjectConfigService = new Mock<ICpsProjectConfigService>();
            treeViewModel = new TreeViewModel(toolWindowHistoryMock.LazyObject());

            itemEvaluationServiceMock.Setup(x => x.EvaluateMacros(It.IsAny<string>(), It.IsAny<IVsHierarchyWrapper>())).Returns<string, IVsHierarchyWrapper>((arg, _) => arg);

            itemAggregationService = new ItemAggregationService(
                itemEvaluationServiceMock.Object,
                vsHelperServiceMock.Object,
                treeViewModel,
                cpsProjectConfigService.Object
            );
        }

        [Fact]
        public async Task GetAllComamndLineItemsForProject_ShouldReturnCmdArgumentsForProject()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;
            var items = new[] { new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg1", isChecked: true) };
            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, false, false, " ", "", "");

            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var result = itemAggregationService.GetAllComamndLineParamsForProject(project).ToList();

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public async Task CreateCommandLineArgsForProject_ShouldCreateCommandLineArgs()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;
            var items = new[] { new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg1", isChecked: true) };
            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, false, false, " ", "", "");

            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var result = itemAggregationService.CreateCommandLineArgsForProject(project);

            // Assert
            Assert.Equal("arg1", result);
        }

        [Fact]
        public async Task GetEnvVarsForProject_ShouldReturnEnvVars()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;

            var items = new[] { new CmdParameter(Guid.NewGuid(), CmdParamType.EnvVar, "Name=Value", isChecked: true) };
            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, isExpanded: false, exclusiveMode: false, delimiter: " ", postfix: "", prefix: "");
            treeViewModel.Projects.Add(projectGuid, cmdProject);

            var parsedEnvVar = new EnvVar { Name = "Name", Value = "Value" };
            itemEvaluationServiceMock.Setup(ies => ies.TryParseEnvVar("Name=Value", out parsedEnvVar)).Returns(true);

            // Act
            var result = itemAggregationService.GetEnvVarsForProject(project).ToList();

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public async Task GetAllComamndLineItemsForProject_ShouldReturnEmpty_WhenNoCmdArgumentIsPresent()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;
            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", new CmdBase[0], false, false, " ", "", "");
            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var result = itemAggregationService.GetAllComamndLineParamsForProject(project).ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetWorkDirForProject_ShouldReturnWorkDir()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;
            var workDirArg = new CmdParameter(Guid.NewGuid(), CmdParamType.WorkDir, "WorkDir", isChecked: true);
            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", new[] { workDirArg }, false, false, " ", "", "");

            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var result = itemAggregationService.GetWorkDirForProject(project);

            // Assert
            Assert.Equal("WorkDir", result);
        }

        [Fact]
        public async Task CreateCommandLineArgsForProject_WithGuid_ShouldCreateCommandLineArgs()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;
            var items = new[] { new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg1", isChecked: true) };
            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, false, false, " ", "", "");

            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var result = itemAggregationService.CreateCommandLineArgsForProject(projectGuid);

            // Assert
            Assert.Equal("arg1", result);
        }

        [Theory]
        [InlineData(" ", "arg1 arg2")]
        [InlineData("", "arg1arg2")]
        [InlineData("|", "arg1|arg2")]
        public async Task CreateCommandLineArgsForProject_ShouldCreateCommandLineArgs_WithMultipleCheckedArguments(string delimiter, string expectedResult)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;

            var items = new List<CmdBase>
            {
                new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg1", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg2", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "uncheckedArg", isChecked: false),
                new CmdParameter(Guid.NewGuid(), CmdParamType.EnvVar, "Name=Value", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.WorkDir, "WorkDir", isChecked: true)
            };

            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, isExpanded: false, exclusiveMode: false, delimiter: delimiter, postfix: "", prefix: "");
            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var result = itemAggregationService.CreateCommandLineArgsForProject(project);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task GetEnvVarsForProject_ShouldReturnMultipleEnvVars_WhenMultipleAreChecked()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;

            var items = new List<CmdBase>
            {
                new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg1", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.EnvVar, "Name1=Value1", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.EnvVar, "Name2=Value2", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.EnvVar, "Unchecked=UncheckedValue", isChecked: false)
            };

            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, isExpanded: false, exclusiveMode: false, delimiter: " ", postfix: "", prefix: "");
            treeViewModel.Projects.Add(projectGuid, cmdProject);

            var parsedEnvVar1 = new EnvVar { Name = "Name1", Value = "Value1" };
            var parsedEnvVar2 = new EnvVar { Name = "Name2", Value = "Value2" };
            itemEvaluationServiceMock.Setup(x => x.TryParseEnvVar("Name1=Value1", out parsedEnvVar1)).Returns(true);
            itemEvaluationServiceMock.Setup(x => x.TryParseEnvVar("Name2=Value2", out parsedEnvVar2)).Returns(true);

            // Act
            var result = itemAggregationService.GetEnvVarsForProject(project);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Value1", result["Name1"]);
            Assert.Equal("Value2", result["Name2"]);
        }

        [Fact]
        public async Task GetEnvVarsForProject_ShouldReturnLastEnvVar_WhenMultipleWithSameNameAreChecked()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;

            var items = new List<CmdBase>
            {
                new CmdParameter(Guid.NewGuid(), CmdParamType.EnvVar, "Name=Value1", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.EnvVar, "Name=Value2", isChecked: true)
            };

            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, isExpanded: false, exclusiveMode: false, delimiter: " ", postfix: "", prefix: "");
            treeViewModel.Projects.Add(projectGuid, cmdProject);

            var parsedEnvVar1 = new EnvVar { Name = "Name", Value = "Value1" };
            var parsedEnvVar2 = new EnvVar { Name = "Name", Value = "Value2" };
            itemEvaluationServiceMock.Setup(x => x.TryParseEnvVar("Name=Value1", out parsedEnvVar1)).Returns(true);
            itemEvaluationServiceMock.Setup(x => x.TryParseEnvVar("Name=Value2", out parsedEnvVar2)).Returns(true);

            // Act
            var result = itemAggregationService.GetEnvVarsForProject(project);

            // Assert
            Assert.Single(result);
            Assert.Equal("Value2", result["Name"]);
        }

        [Fact]
        public async Task CreateCommandLineArgsForProject_ShouldReturnLastWorkDir_WhenMultipleAreChecked()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;

            var items = new List<CmdBase>
            {
                new CmdParameter(Guid.NewGuid(), CmdParamType.WorkDir, "WorkDir1", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.WorkDir, "WorkDir2", isChecked: true)
            };

            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, isExpanded: false, exclusiveMode: false, delimiter: " ", postfix: "", prefix: "");
            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var resultWorkDir = itemAggregationService.GetWorkDirForProject(project);

            // Assert
            Assert.Equal("WorkDir2", resultWorkDir);
        }

        [Fact]
        public async Task CreateCommandLineArgsForProject_ShouldHandleGroupsAndDelimiters()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Arrange
            var projectGuid = Guid.NewGuid();
            var project = new Mock<IVsHierarchyWrapper>().Register(vsHelperServiceMock, projectGuid).Object;

            var groupItems = new List<CmdBase>
            {
                new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg2", isChecked: true),
                new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg3", isChecked: true)
            };

            var cmdGroup = new CmdGroup("GroupName", groupItems, isExpanded: true, exclusiveMode: false, delimiter: ",", prefix: "{", postfix: "}");

            var items = new List<CmdBase>
            {
                new CmdParameter(Guid.NewGuid(), CmdParamType.CmdArg, "arg1", isChecked: true),
                cmdGroup
            };

            var cmdProject = new CmdProject(projectGuid, Guid.Empty, "TestProject", items, isExpanded: false, exclusiveMode: false, delimiter: " ", prefix: "[", postfix: "]");
            treeViewModel.Projects.Add(projectGuid, cmdProject);

            // Act
            var result = itemAggregationService.CreateCommandLineArgsForProject(project);

            // Assert
            Assert.Equal("[arg1 {arg2,arg3}]", result);
        }




        // Add additional test methods to cover other methods and scenarios.
    }
}
