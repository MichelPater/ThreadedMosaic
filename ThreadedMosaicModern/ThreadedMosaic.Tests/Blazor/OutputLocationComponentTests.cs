using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ThreadedMosaic.BlazorServer.Components.Shared;

namespace ThreadedMosaic.Tests.Blazor
{
    /// <summary>
    /// Tests for OutputLocationComponent with clean directory picker
    /// </summary>
    public class OutputLocationComponentTests : TestContext
    {
        [Fact]
        public void OutputLocationComponent_RendersCorrectly()
        {
            // Act
            var component = RenderComponent<OutputLocationComponent>();

            // Assert
            component.Should().NotBeNull();
            
            var label = component.Find("label");
            label.Should().NotBeNull();
            label.TextContent.Should().Contain("Output Location");
            
            var fileInput = component.Find("input[type=file]");
            fileInput.Should().NotBeNull();
            fileInput.GetAttribute("class").Should().Contain("form-control");
            fileInput.HasAttribute("webkitdirectory").Should().BeTrue();
            fileInput.HasAttribute("directory").Should().BeTrue();
        }

        [Fact]
        public void OutputLocationComponent_DefaultState_ShowsNoErrors()
        {
            // Act
            var component = RenderComponent<OutputLocationComponent>();

            // Assert
            var errorElements = component.FindAll(".text-danger");
            errorElements.Should().BeEmpty();
        }

        [Fact]
        public void OutputLocationComponent_WithDefaultPath_ShowsSuccess()
        {
            // Act
            var component = RenderComponent<OutputLocationComponent>();

            // Assert - Should show default desktop path
            var successElements = component.FindAll(".text-success");
            successElements.Should().NotBeEmpty();
            
            var successText = successElements.First().TextContent;
            successText.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void OutputLocationComponent_WithSelectedPath_ShowsFileName()
        {
            // Arrange
            var testPath = @"MyOutputFolder";
            
            // Act
            var component = RenderComponent<OutputLocationComponent>(parameters => parameters
                .Add(p => p.OutputPath, testPath));

            // Assert
            var successElements = component.FindAll(".text-success");
            successElements.Should().NotBeEmpty();
            
            var successText = successElements.First().TextContent;
            successText.Should().Contain("MyOutputFolder");
        }

        [Fact]
        public void OutputLocationComponent_InputGroup_HasCorrectStructure()
        {
            // Act
            var component = RenderComponent<OutputLocationComponent>();

            // Assert
            var inputGroup = component.Find(".input-group");
            inputGroup.Should().NotBeNull();
            
            // Should have the file input
            var fileInput = inputGroup.QuerySelector("input[type=file]");
            fileInput.Should().NotBeNull();
        }

        [Fact]
        public void OutputLocationComponent_ParameterBinding_WorksCorrectly()
        {
            // Arrange
            string outputPath = null;
            var component = RenderComponent<OutputLocationComponent>(parameters => parameters
                .Add(p => p.OutputPath, "")
                .Add(p => p.OutputPathChanged, (string path) => outputPath = path));

            // Assert - Component should be initialized without errors
            component.Should().NotBeNull();
            
            // The file input should have directory attributes
            var fileInput = component.Find("input[type=file]");
            fileInput.HasAttribute("webkitdirectory").Should().BeTrue();
            fileInput.HasAttribute("directory").Should().BeTrue();
        }

        [Fact]
        public void OutputLocationComponent_ClearButton_AppearsWithSelection()
        {
            // Arrange
            var testPath = @"MyOutputFolder";
            
            // Act
            var component = RenderComponent<OutputLocationComponent>(parameters => parameters
                .Add(p => p.OutputPath, testPath));

            // Assert
            var clearButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Clear"));
            clearButton.Should().NotBeNull();
            clearButton.GetAttribute("class").Should().Contain("btn-outline-danger");
        }

        [Fact]
        public void OutputLocationComponent_FileInput_HasCorrectAttributes()
        {
            // Act
            var component = RenderComponent<OutputLocationComponent>();

            // Assert
            var fileInput = component.Find("input[type=file]");
            
            // Check directory selection attributes
            fileInput.HasAttribute("webkitdirectory").Should().BeTrue();
            fileInput.HasAttribute("directory").Should().BeTrue();
            
            // Output location doesn't need multiple files like seed directory
            fileInput.HasAttribute("multiple").Should().BeFalse();
        }

        [Fact]
        public void OutputLocationComponent_HelpText_IsPresent()
        {
            // Act
            var component = RenderComponent<OutputLocationComponent>();

            // Assert
            var helpText = component.FindAll(".form-text").LastOrDefault();
            helpText.Should().NotBeNull();
            helpText.TextContent.Should().Contain("Select a directory where your mosaic will be saved");
            helpText.TextContent.Should().Contain("JPEG, PNG, BMP, WebP");
        }

        [Fact]
        public void OutputLocationComponent_HasClearButton_WithDefaultPath()
        {
            // Act - Component initializes with default desktop path
            var component = RenderComponent<OutputLocationComponent>();

            // Assert - Should have Clear button since default path is set
            var clearButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Clear"));
            clearButton.Should().NotBeNull("Component sets default path, so Clear button should be present");
        }

        [Fact]
        public void OutputLocationComponent_CleanInterface_OnlyNativeFileInput()
        {
            // Act
            var component = RenderComponent<OutputLocationComponent>();

            // Assert
            // Should only have one file input
            var fileInputs = component.FindAll("input[type=file]");
            fileInputs.Should().HaveCount(1);
            
            // Should not have old-style text inputs
            var textInputs = component.FindAll("input[type=text]");
            textInputs.Should().BeEmpty();
            
            // Should have at most one button (Clear button when directory is selected)
            var buttons = component.FindAll("button");
            buttons.Should().HaveCountLessOrEqualTo(1);
        }
    }
}