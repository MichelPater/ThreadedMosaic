using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ThreadedMosaic.BlazorServer.Components.Shared;

namespace ThreadedMosaic.Tests.Blazor
{
    /// <summary>
    /// Tests for FileUploadComponent Blazor component
    /// </summary>
    public class FileUploadComponentTests : TestContext
    {
        [Fact]
        public void FileUploadComponent_RendersCorrectly()
        {
            // Act
            var component = RenderComponent<FileUploadComponent>(parameters => parameters
                .Add(p => p.Label, "Upload Master Image")
                .Add(p => p.Accept, "image/*"));

            // Assert
            component.Should().NotBeNull();
            component.Find("label").TextContent.Should().Be("Upload Master Image");
            
            var fileInput = component.Find("input[type=file]");
            fileInput.Should().NotBeNull();
            fileInput.GetAttribute("accept").Should().Be("image/*");
            fileInput.GetAttribute("class").Should().Contain("form-control");
        }

        [Fact]
        public void FileUploadComponent_DefaultLabel_DisplaysCorrectly()
        {
            // Act
            var component = RenderComponent<FileUploadComponent>();

            // Assert
            component.Find("label").TextContent.Should().Be("Select File");
            var fileInput = component.Find("input[type=file]");
            fileInput.GetAttribute("accept").Should().Be("*/*");
        }

        [Fact]
        public void FileUploadComponent_WithSelectedFile_ShowsSuccessMessage()
        {
            // Arrange
            var mockFile = new Mock<IBrowserFile>();
            mockFile.Setup(f => f.Name).Returns("test-image.jpg");
            mockFile.Setup(f => f.Size).Returns(1024 * 1024); // 1MB

            // Act
            var component = RenderComponent<FileUploadComponent>(parameters => parameters
                .Add(p => p.SelectedFile, mockFile.Object));

            // Assert
            var successMessage = component.Find(".text-success");
            successMessage.Should().NotBeNull();
            successMessage.TextContent.Should().Contain("test-image.jpg");
            successMessage.TextContent.Should().Contain("1.00 MB");
        }

        [Fact]
        public void FileUploadComponent_WithError_ShowsErrorMessage()
        {
            // Act
            var component = RenderComponent<FileUploadComponent>();
            
            // Simulate error by invoking the component's error handling
            var instance = component.Instance;
            var errorField = typeof(FileUploadComponent).GetField("errorMessage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            errorField?.SetValue(instance, "File too large");
            component.Render();

            // Assert
            var errorMessage = component.Find(".text-danger");
            errorMessage.Should().NotBeNull();
            errorMessage.TextContent.Should().Contain("File too large");
        }

        [Theory]
        [InlineData(1024, "1.00 KB")]
        [InlineData(1024 * 1024, "1.00 MB")]
        [InlineData(5 * 1024 * 1024, "5.00 MB")]
        public void FileUploadComponent_FormatFileSize_WorksCorrectly(long sizeBytes, string expectedFormat)
        {
            // Arrange
            var mockFile = new Mock<IBrowserFile>();
            mockFile.Setup(f => f.Name).Returns("test.jpg");
            mockFile.Setup(f => f.Size).Returns(sizeBytes);

            // Act
            var component = RenderComponent<FileUploadComponent>(parameters => parameters
                .Add(p => p.SelectedFile, mockFile.Object));

            // Assert
            var successMessage = component.Find(".text-success");
            successMessage.TextContent.Should().Contain(expectedFormat);
        }

        [Fact]
        public void FileUploadComponent_CustomAcceptType_SetsCorrectAttribute()
        {
            // Act
            var component = RenderComponent<FileUploadComponent>(parameters => parameters
                .Add(p => p.Accept, ".jpg,.png,.gif"));

            // Assert
            var fileInput = component.Find("input[type=file]");
            fileInput.GetAttribute("accept").Should().Be(".jpg,.png,.gif");
        }
    }
}