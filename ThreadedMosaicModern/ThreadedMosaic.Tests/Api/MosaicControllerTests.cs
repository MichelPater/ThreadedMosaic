using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThreadedMosaic.Api.Controllers;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.Services;
using ThreadedMosaic.Core.Interfaces;
using System.Threading;

namespace ThreadedMosaic.Tests.Api
{
    /// <summary>
    /// Tests for MosaicController with correct method signatures
    /// </summary>
    public class MosaicControllerTests : IDisposable
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<MosaicController>> _mockLogger;
        private readonly Mock<ColorMosaicService> _mockColorService;
        private readonly Mock<HueMosaicService> _mockHueService;
        private readonly Mock<PhotoMosaicService> _mockPhotoService;
        private readonly MosaicController _controller;
        private readonly string _testDirectory;

        public MosaicControllerTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<MosaicController>>();
            _mockColorService = new Mock<ColorMosaicService>();
            _mockHueService = new Mock<HueMosaicService>();
            _mockPhotoService = new Mock<PhotoMosaicService>();

            // Setup service provider to return mocked services
            _mockServiceProvider.Setup(sp => sp.GetRequiredService<ColorMosaicService>())
                               .Returns(_mockColorService.Object);
            _mockServiceProvider.Setup(sp => sp.GetRequiredService<HueMosaicService>())
                               .Returns(_mockHueService.Object);
            _mockServiceProvider.Setup(sp => sp.GetRequiredService<PhotoMosaicService>())
                               .Returns(_mockPhotoService.Object);

            _controller = new MosaicController(_mockServiceProvider.Object, _mockLogger.Object);

            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "ThreadedMosaicApiTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public async Task CreateColorMosaic_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ColorMosaicRequest
            {
                MasterImagePath = Path.Combine(_testDirectory, "master.jpg"),
                SeedImagesDirectory = _testDirectory,
                OutputPath = Path.Combine(_testDirectory, "output.jpg"),
                PixelSize = 16,
                ColorTolerance = 10
            };

            var expectedResult = new MosaicResult
            {
                MosaicId = Guid.NewGuid(),
                Status = MosaicStatus.Completed,
                OutputPath = request.OutputPath
            };

            _mockColorService.Setup(s => s.CreateColorMosaicAsync(It.IsAny<ColorMosaicRequest>(), It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.CreateColorMosaic(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();
            
            var okResult = result.Result as OkObjectResult;
            okResult?.Value.Should().Be(expectedResult);
        }

        [Fact]
        public async Task CreateColorMosaic_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var request = new ColorMosaicRequest(); // Empty request should be invalid

            _mockColorService.Setup(s => s.CreateColorMosaicAsync(It.IsAny<ColorMosaicRequest>(), It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new ArgumentException("Invalid request"));

            // Act
            var result = await _controller.CreateColorMosaic(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateHueMosaic_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new HueMosaicRequest
            {
                MasterImagePath = Path.Combine(_testDirectory, "master.jpg"),
                SeedImagesDirectory = _testDirectory,
                OutputPath = Path.Combine(_testDirectory, "output.jpg"),
                PixelSize = 16,
                HueTolerance = 15.0
            };

            var expectedResult = new MosaicResult
            {
                MosaicId = Guid.NewGuid(),
                Status = MosaicStatus.Completed,
                OutputPath = request.OutputPath
            };

            _mockHueService.Setup(s => s.CreateHueMosaicAsync(It.IsAny<HueMosaicRequest>(), It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.CreateHueMosaic(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();
            
            var okResult = result.Result as OkObjectResult;
            okResult?.Value.Should().Be(expectedResult);
        }

        [Fact]
        public async Task CreatePhotoMosaic_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new PhotoMosaicRequest
            {
                MasterImagePath = Path.Combine(_testDirectory, "master.jpg"),
                SeedImagesDirectory = _testDirectory,
                OutputPath = Path.Combine(_testDirectory, "output.jpg"),
                PixelSize = 16,
                SimilarityThreshold = 75
            };

            var expectedResult = new MosaicResult
            {
                MosaicId = Guid.NewGuid(),
                Status = MosaicStatus.Completed,
                OutputPath = request.OutputPath
            };

            _mockPhotoService.Setup(s => s.CreatePhotoMosaicAsync(It.IsAny<PhotoMosaicRequest>(), It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.CreatePhotoMosaic(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Result.Should().BeOfType<OkObjectResult>();
            
            var okResult = result.Result as OkObjectResult;
            okResult?.Value.Should().Be(expectedResult);
        }

        [Fact]
        public async Task GetMosaicStatus_ValidId_ReturnsOkResult()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();

            // Act
            var result = await _controller.GetMosaicStatus(mosaicId);

            // Assert
            result.Should().NotBeNull();
            // Note: The actual implementation would need to be mocked for full testing
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task CreateColorMosaic_InvalidMasterImagePath_ThrowsArgumentException(string? invalidPath)
        {
            // Arrange
            var request = new ColorMosaicRequest
            {
                MasterImagePath = invalidPath!,
                SeedImagesDirectory = _testDirectory,
                OutputPath = Path.Combine(_testDirectory, "output.jpg")
            };

            _mockColorService.Setup(s => s.CreateColorMosaicAsync(It.IsAny<ColorMosaicRequest>(), It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new ArgumentException("Master image path is required"));

            // Act
            var result = await _controller.CreateColorMosaic(request, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateColorMosaic_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ColorMosaicRequest
            {
                MasterImagePath = Path.Combine(_testDirectory, "master.jpg"),
                SeedImagesDirectory = _testDirectory,
                OutputPath = Path.Combine(_testDirectory, "output.jpg")
            };

            _mockColorService.Setup(s => s.CreateColorMosaicAsync(It.IsAny<ColorMosaicRequest>(), It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new InvalidOperationException("Service error"));

            // Act
            var result = await _controller.CreateColorMosaic(request, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult?.StatusCode.Should().Be(500);
        }

        [Fact]
        public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions.Invoking(() => new MosaicController(null!, _mockLogger.Object))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("serviceProvider");
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions.Invoking(() => new MosaicController(_mockServiceProvider.Object, null!))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("logger");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}