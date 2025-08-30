using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using System.IO;
using ThreadedMosaic.BlazorServer.Components.Shared;

namespace ThreadedMosaic.Tests.Blazor
{
    /// <summary>
    /// Tests for simplified SeedFolderComponent directory picker
    /// </summary>
    public class SeedFolderComponentTests : TestContext
    {
        public SeedFolderComponentTests()
        {
            // Setup JSRuntime mock for JavaScript interop
            var jsRuntimeMock = new Mock<IJSRuntime>();
            Services.AddSingleton(jsRuntimeMock.Object);
        }

        [Fact]
        public void SeedFolderComponent_RendersCorrectly()
        {
            // Act
            var component = RenderComponent<SeedFolderComponent>();

            // Assert
            component.Should().NotBeNull();
            
            var label = component.Find("label");
            label.Should().NotBeNull();
            label.TextContent.Should().Contain("Seed Images Directory");
            
            var fileInput = component.Find("input[type=file]");
            fileInput.Should().NotBeNull();
            fileInput.GetAttribute("class").Should().Contain("form-control");
            fileInput.HasAttribute("webkitdirectory").Should().BeTrue();
            fileInput.HasAttribute("directory").Should().BeTrue();
            
            // There should be no additional browse buttons - only the native file input
            var buttons = component.FindAll("button");
            buttons.Should().BeEmpty(); // No Clear button when nothing is selected
        }

        [Fact]
        public void SeedFolderComponent_DefaultState_ShowsNoErrors()
        {
            // Act
            var component = RenderComponent<SeedFolderComponent>();

            // Assert
            var errorElements = component.FindAll(".text-danger");
            errorElements.Should().BeEmpty();
            
            var successElements = component.FindAll(".text-success");
            successElements.Should().BeEmpty();
        }

        [Fact]
        public void SeedFolderComponent_WithSelectedDirectory_ShowsFileName()
        {
            // Arrange
            var testPath = @"MyImages";
            
            // Act
            var component = RenderComponent<SeedFolderComponent>(parameters => parameters
                .Add(p => p.SelectedDirectory, testPath));

            // Assert
            component.Should().NotBeNull();
            
            var successElements = component.FindAll(".text-success");
            successElements.Should().NotBeEmpty();
            
            var successText = successElements.First().TextContent;
            successText.Should().Contain("MyImages");
        }

        [Fact]
        public void SeedFolderComponent_InputGroup_HasCorrectStructure()
        {
            // Act
            var component = RenderComponent<SeedFolderComponent>();

            // Assert
            var inputGroup = component.Find(".input-group");
            inputGroup.Should().NotBeNull();
            
            // Should only have the native file input - no additional browse buttons
            var fileInput = inputGroup.QuerySelector("input[type=file]");
            fileInput.Should().NotBeNull();
            
            // No browse button should exist
            var browseButtons = inputGroup.QuerySelectorAll("button");
            browseButtons.Should().BeEmpty();
        }

        [Fact]
        public void SeedFolderComponent_ParameterBinding_WorksCorrectly()
        {
            // Arrange
            string selectedDir = null;
            var component = RenderComponent<SeedFolderComponent>(parameters => parameters
                .Add(p => p.SelectedDirectory, "")
                .Add(p => p.SelectedDirectoryChanged, (string dir) => selectedDir = dir));

            // Assert - Component should be initialized without errors
            component.Should().NotBeNull();
            
            // The file input should have directory attributes
            var fileInput = component.Find("input[type=file]");
            fileInput.HasAttribute("webkitdirectory").Should().BeTrue();
            fileInput.HasAttribute("directory").Should().BeTrue();
            fileInput.HasAttribute("multiple").Should().BeTrue();
        }

        [Fact]
        public void SeedFolderComponent_ClearButton_AppearsWithSelection()
        {
            // Arrange
            var testPath = @"MyImages";
            
            // Act
            var component = RenderComponent<SeedFolderComponent>(parameters => parameters
                .Add(p => p.SelectedDirectory, testPath));

            // Assert
            var clearButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Clear"));
            clearButton.Should().NotBeNull();
            clearButton.GetAttribute("class").Should().Contain("btn-outline-danger");
        }

        [Fact]
        public void SeedFolderComponent_FileInput_HasCorrectAttributes()
        {
            // Act
            var component = RenderComponent<SeedFolderComponent>();

            // Assert
            var fileInput = component.Find("input[type=file]");
            
            // Check directory selection attributes
            fileInput.HasAttribute("webkitdirectory").Should().BeTrue();
            fileInput.HasAttribute("directory").Should().BeTrue();
            fileInput.HasAttribute("multiple").Should().BeTrue();
            
            // Check accept attribute for images
            fileInput.GetAttribute("accept").Should().Be("image/*");
        }

        [Fact]
        public void SeedFolderComponent_HelpText_IsPresent()
        {
            // Act
            var component = RenderComponent<SeedFolderComponent>();

            // Assert
            var helpText = component.FindAll(".form-text").LastOrDefault();
            helpText.Should().NotBeNull();
            helpText.TextContent.Should().Contain("Select a directory containing images to use as tiles");
        }

        [Fact]
        public void SeedFolderComponent_NoClearButton_WithoutSelection()
        {
            // Act
            var component = RenderComponent<SeedFolderComponent>();

            // Assert
            var clearButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Clear"));
            clearButton.Should().BeNull();
        }

        [Fact]
        public void SeedFolderComponent_SimplifiedInterface_OnlyNativeFileInput()
        {
            // Act
            var component = RenderComponent<SeedFolderComponent>();

            // Assert
            // Should not have any complex interface elements
            var cards = component.FindAll(".card");
            cards.Should().BeEmpty();
            
            var buttonGroups = component.FindAll(".btn-group");
            buttonGroups.Should().BeEmpty();
            
            var textInputs = component.FindAll("input[type=text]");
            textInputs.Should().BeEmpty();
            
            // Should only have one file input
            var fileInputs = component.FindAll("input[type=file]");
            fileInputs.Should().HaveCount(1);
            
            // Should have no buttons when no directory is selected
            var buttons = component.FindAll("button");
            buttons.Should().BeEmpty();
        }
    }
}