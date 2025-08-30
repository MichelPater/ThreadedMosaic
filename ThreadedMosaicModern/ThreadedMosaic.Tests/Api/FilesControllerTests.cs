using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using ThreadedMosaic.Api.Controllers;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Tests.Api
{
    /// <summary>
    /// Tests for FilesController upload functionality
    /// </summary>
    public class FilesControllerTests
    {
        private readonly Mock<IFileOperations> _fileOperationsMock;
        private readonly Mock<ILogger<FilesController>> _loggerMock;
        private readonly FilesController _controller;

        public FilesControllerTests()
        {
            _fileOperationsMock = new Mock<IFileOperations>();
            _loggerMock = new Mock<ILogger<FilesController>>();
            _controller = new FilesController(_fileOperationsMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task UploadFiles_WithValidMasterImage_ReturnsSuccessResult()
        {
            // Arrange
            var fileContent = "fake image content";
            var fileName = "test-image.jpg";
            var contentType = "image/jpeg";
            
            var formFile = CreateMockFormFile(fileName, contentType, fileContent);
            var files = new FormFileCollection { formFile };

            // Act
            var result = await _controller.UploadFiles(files, "master");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<OkObjectResult>();
            
            var okResult = (OkObjectResult)result;
            okResult.Value.Should().BeOfType<FileUploadResult>();
            
            var uploadResult = (FileUploadResult)okResult.Value!;
            uploadResult.Files.Should().HaveCount(1);
            uploadResult.Message.Should().Contain("Successfully uploaded 1 master files");
            
            var uploadedFile = uploadResult.Files.First();
            uploadedFile.OriginalName.Should().Be(fileName);
            uploadedFile.ContentType.Should().Be(contentType);
            uploadedFile.Size.Should().Be(fileContent.Length);
            uploadedFile.FilePath.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task UploadFiles_WithInvalidType_ReturnsBadRequest()
        {
            // Arrange
            var formFile = CreateMockFormFile("test.jpg", "image/jpeg", "content");
            var files = new FormFileCollection { formFile };

            // Act
            var result = await _controller.UploadFiles(files, "invalid");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            
            var badRequestResult = (BadRequestObjectResult)result;
            var errorResponse = badRequestResult.Value;
            errorResponse.Should().NotBeNull();
        }

        [Fact]
        public async Task UploadFiles_WithNoFiles_ReturnsBadRequest()
        {
            // Arrange
            var files = new FormFileCollection();

            // Act
            var result = await _controller.UploadFiles(files, "master");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UploadFiles_WithUnsupportedFileType_ReturnsBadRequest()
        {
            // Arrange
            var formFile = CreateMockFormFile("test.txt", "text/plain", "content");
            var files = new FormFileCollection { formFile };

            // Act
            var result = await _controller.UploadFiles(files, "master");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            
            var badRequestResult = (BadRequestObjectResult)result;
            var errorResponse = badRequestResult.Value;
            errorResponse.Should().NotBeNull();
        }

        [Theory]
        [InlineData("master")]
        [InlineData("seed")]
        public async Task UploadFiles_WithValidType_AcceptsType(string type)
        {
            // Arrange
            var formFile = CreateMockFormFile("test.jpg", "image/jpeg", "content");
            var files = new FormFileCollection { formFile };

            // Act
            var result = await _controller.UploadFiles(files, type);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        private static IFormFile CreateMockFormFile(string fileName, string contentType, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            
            var formFile = new Mock<IFormFile>();
            formFile.Setup(f => f.FileName).Returns(fileName);
            formFile.Setup(f => f.ContentType).Returns(contentType);
            formFile.Setup(f => f.Length).Returns(bytes.Length);
            formFile.Setup(f => f.OpenReadStream()).Returns(stream);
            formFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                   .Returns((Stream target, CancellationToken token) => stream.CopyToAsync(target, token));
            
            return formFile.Object;
        }
    }
}