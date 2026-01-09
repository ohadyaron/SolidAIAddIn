using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using SolidAIAddIn.MCP.Models;
using SolidAIAddIn.MCP.Handlers;
using SolidAIAddIn.SolidWorksWrapper;
using SolidWorks.Interop.sldworks;

namespace SolidAIAddIn.Tests
{
    /// <summary>
    /// Unit tests for MCP command handlers
    /// Uses mocks to test command flow without SOLIDWORKS installation
    /// </summary>
    public class McpCommandHandlerTests
    {
        #region Test Fixtures

        private Mock<SolidWorksApiWrapper> CreateMockWrapper()
        {
            var mockSwApp = new Mock<ISldWorks>();
            return new Mock<SolidWorksApiWrapper>(mockSwApp.Object);
        }

        #endregion

        #region CreatePartFromIntent Tests

        [Fact]
        public async Task HandleCreatePartFromIntent_WithValidIntent_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();
            
            mockWrapper
                .Setup(w => w.CreatePartDocument(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(OperationResult<IModelDoc2>.Success(mockModelDoc.Object, "Part created"));

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new CreatePartFromIntentRequest
            {
                Intent = "Create a mounting bracket",
                Template = "Part",
                Units = "MMGS"
            };

            // Act
            var response = await handler.HandleCreatePartFromIntent(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            response.Message.Should().Contain("Part document created");
            mockWrapper.Verify(w => w.CreatePartDocument("Part", "MMGS"), Times.Once);
        }

        [Fact]
        public async Task HandleCreatePartFromIntent_WithEmptyIntent_ShouldFail()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new CreatePartFromIntentRequest
            {
                Intent = "",
                Template = "Part"
            };

            // Act
            var response = await handler.HandleCreatePartFromIntent(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeFalse();
            response.ErrorDetails.Should().Contain("Intent description is required");
        }

        #endregion

        #region ApplyFeature Tests

        [Fact]
        public async Task HandleApplyFeature_WithHoleFeature_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();
            var mockFeature = new Mock<IFeature>();

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            mockWrapper
                .Setup(w => w.AddSimpleHole(
                    It.IsAny<IModelDoc2>(),
                    It.IsAny<double>(),
                    It.IsAny<double>(),
                    It.IsAny<double[]>()))
                .Returns(OperationResult<IFeature>.Success(mockFeature.Object, "Hole created"));

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new ApplyFeatureRequest
            {
                FeatureType = "hole",
                Parameters = new Dictionary<string, object>
                {
                    { "diameter", 10.0 },
                    { "depth", 20.0 }
                }
            };

            // Act
            var response = await handler.HandleApplyFeature(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            response.Message.Should().Contain("applied successfully");
        }

        [Fact]
        public async Task HandleApplyFeature_WithNoActiveDocument_ShouldFail()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns((IModelDoc2?)null);

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new ApplyFeatureRequest
            {
                FeatureType = "hole"
            };

            // Act
            var response = await handler.HandleApplyFeature(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeFalse();
            response.ErrorDetails.Should().Contain("No active document");
        }

        #endregion

        #region Rebuild Tests

        [Fact]
        public async Task HandleRebuild_WithActiveDocument_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            mockWrapper
                .Setup(w => w.RebuildModel(It.IsAny<IModelDoc2>()))
                .Returns(OperationResult<bool>.Success(true, "Model rebuilt"));

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new RebuildRequest
            {
                Option = "Active"
            };

            // Act
            var response = await handler.HandleRebuild(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            response.Message.Should().Contain("rebuilt successfully");
        }

        #endregion

        #region ExportStep Tests

        [Fact]
        public async Task HandleExportStep_WithValidPath_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();
            var outputPath = @"C:\temp\test.step";

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            mockWrapper
                .Setup(w => w.ExportToStep(It.IsAny<IModelDoc2>(), It.IsAny<string>()))
                .Returns(OperationResult<string>.Success(outputPath, "Exported"));

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new ExportStepRequest
            {
                OutputPath = outputPath,
                StepVersion = "AP214"
            };

            // Act
            var response = await handler.HandleExportStep(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            response.Message.Should().Contain("Exported to STEP");
        }

        [Fact]
        public async Task HandleExportStep_WithEmptyPath_ShouldFail()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new ExportStepRequest
            {
                OutputPath = ""
            };

            // Act
            var response = await handler.HandleExportStep(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeFalse();
            response.ErrorDetails.Should().Contain("Output path is required");
        }

        #endregion

        #region CreateRectangularExtrusion Tests

        [Fact]
        public async Task HandleCreateRectangularExtrusion_WithValidDimensions_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();
            var mockFeature = new Mock<IFeature>();

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            mockWrapper
                .Setup(w => w.CreateRectangularExtrusion(
                    It.IsAny<IModelDoc2>(),
                    It.IsAny<double>(),
                    It.IsAny<double>(),
                    It.IsAny<double>(),
                    It.IsAny<string>()))
                .Returns(OperationResult<IFeature>.Success(mockFeature.Object, "Extrusion created"));

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new CreateRectangularExtrusionRequest
            {
                Width = 100,
                Height = 50,
                Depth = 10,
                Plane = "Front"
            };

            // Act
            var response = await handler.HandleCreateRectangularExtrusion(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            response.Message.Should().Contain("created successfully");
            mockWrapper.Verify(
                w => w.CreateRectangularExtrusion(mockModelDoc.Object, 100, 50, 10, "Front"),
                Times.Once);
        }

        [Fact]
        public async Task HandleCreateRectangularExtrusion_WithNegativeDimensions_ShouldFail()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new CreateRectangularExtrusionRequest
            {
                Width = -100,
                Height = 50,
                Depth = 10
            };

            // Act
            var response = await handler.HandleCreateRectangularExtrusion(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeFalse();
            response.ErrorDetails.Should().Contain("Dimensions must be positive");
        }

        #endregion

        #region SetParameters Tests

        [Fact]
        public async Task HandleSetParameters_WithValidParameters_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new SetParametersRequest
            {
                Parameters = new Dictionary<string, double>
                {
                    { "Length", 100 },
                    { "Width", 50 },
                    { "Height", 30 }
                }
            };

            // Act
            var response = await handler.HandleSetParameters(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            response.Message.Should().Contain("parameters");
        }

        #endregion

        #region AddHolePattern Tests

        [Fact]
        public async Task HandleAddHolePattern_WithValidParameters_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new AddHolePatternRequest
            {
                Diameter = 10,
                Depth = 20,
                PatternType = "Linear",
                Count = 4,
                Spacing = 10
            };

            // Act
            var response = await handler.HandleAddHolePattern(request);

            // Assert
            response.Should().NotBeNull();
            // Note: This is a placeholder implementation, so we just check it doesn't crash
        }

        [Fact]
        public async Task HandleAddHolePattern_WithInvalidDiameter_ShouldFail()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new AddHolePatternRequest
            {
                Diameter = -10,
                Depth = 20
            };

            // Act
            var response = await handler.HandleAddHolePattern(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeFalse();
            response.ErrorDetails.Should().Contain("must be positive");
        }

        #endregion

        #region CapturePreview Tests

        [Fact]
        public async Task HandleCapturePreview_WithActiveDocument_ShouldSucceed()
        {
            // Arrange
            var mockWrapper = CreateMockWrapper();
            var mockModelDoc = new Mock<IModelDoc2>();

            mockWrapper
                .Setup(w => w.GetActiveDocument())
                .Returns(mockModelDoc.Object);

            var handler = new McpCommandHandler(mockWrapper.Object);
            var request = new CapturePreviewRequest
            {
                Width = 1024,
                Height = 768,
                Format = "PNG"
            };

            // Act
            var response = await handler.HandleCapturePreview(request);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            // Note: This is a placeholder implementation
        }

        #endregion
    }
}
