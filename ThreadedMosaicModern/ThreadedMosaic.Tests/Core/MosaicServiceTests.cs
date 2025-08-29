using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.Services;
using Xunit;

namespace ThreadedMosaic.Tests.Core
{
    /// <summary>
    /// Comprehensive tests for all three mosaic services demonstrating Phase 7.4 service layer testing
    /// </summary>
    public class MosaicServiceTests : IDisposable
    {
        private readonly Mock<IImageProcessingService> _mockImageProcessingService;
        private readonly Mock<IFileOperations> _mockFileOperations;
        private readonly Mock<ILogger<ColorMosaicService>> _mockColorLogger;
        private readonly Mock<ILogger<HueMosaicService>> _mockHueLogger;
        private readonly Mock<ILogger<PhotoMosaicService>> _mockPhotoLogger;
        private readonly string _testDirectory;

        public MosaicServiceTests()
        {
            _mockImageProcessingService = new Mock<IImageProcessingService>();
            _mockFileOperations = new Mock<IFileOperations>();
            _mockColorLogger = new Mock<ILogger<ColorMosaicService>>();
            _mockHueLogger = new Mock<ILogger<HueMosaicService>>();
            _mockPhotoLogger = new Mock<ILogger<PhotoMosaicService>>();

            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "MosaicServiceTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        #region ColorMosaicService Tests

        [Fact]
        public void ColorMosaicService_Constructor_ValidParameters_CreatesInstance()
        {
            // Act
            var service = new ColorMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockColorLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IColorMosaicService>();
            service.Should().BeAssignableTo<IMosaicService>();
        }

        [Fact]
        public void ColorMosaicService_Constructor_NullImageProcessingService_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions.Invoking(() => new ColorMosaicService(null!, _mockFileOperations.Object, _mockColorLogger.Object))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("imageProcessingService");
        }

        [Fact]
        public async Task ColorMosaicService_CreateColorMosaicAsync_ValidRequest_ReturnsResult()
        {
            // Arrange
            var service = new ColorMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockColorLogger.Object);
            var request = CreateValidColorMosaicRequest();

            // Setup mocks
            _mockFileOperations.Setup(fo => fo.IsValidImageFileAsync(request.MasterImagePath)).ReturnsAsync(true);
            _mockImageProcessingService.Setup(ips => ips.LoadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                      .ReturnsAsync(new LoadedImage());

            // Act
            var result = await service.CreateColorMosaicAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.MosaicId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task ColorMosaicService_CreateHueMosaicAsync_ThrowsNotSupportedException()
        {
            // Arrange
            var service = new ColorMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockColorLogger.Object);
            var request = new HueMosaicRequest 
            { 
                MasterImagePath = "test.jpg", 
                SeedImagesDirectory = "seeds" 
            };

            // Act & Assert
            await FluentActions.Invoking(async () => await service.CreateHueMosaicAsync(request))
                               .Should().ThrowAsync<NotSupportedException>()
                               .WithMessage("Use HueMosaicService for hue-based mosaics");
        }

        [Fact]
        public async Task ColorMosaicService_CreatePhotoMosaicAsync_ThrowsNotSupportedException()
        {
            // Arrange
            var service = new ColorMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockColorLogger.Object);
            var request = new PhotoMosaicRequest 
            { 
                MasterImagePath = "test.jpg", 
                SeedImagesDirectory = "seeds" 
            };

            // Act & Assert
            await FluentActions.Invoking(async () => await service.CreatePhotoMosaicAsync(request))
                               .Should().ThrowAsync<NotSupportedException>()
                               .WithMessage("Use PhotoMosaicService for photo-based mosaics");
        }

        #endregion

        #region HueMosaicService Tests

        [Fact]
        public void HueMosaicService_Constructor_ValidParameters_CreatesInstance()
        {
            // Act
            var service = new HueMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockHueLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IHueMosaicService>();
            service.Should().BeAssignableTo<IMosaicService>();
        }

        [Fact]
        public async Task HueMosaicService_CreateHueMosaicAsync_ValidRequest_ReturnsResult()
        {
            // Arrange
            var service = new HueMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockHueLogger.Object);
            var request = CreateValidHueMosaicRequest();

            // Setup mocks
            _mockFileOperations.Setup(fo => fo.IsValidImageFileAsync(request.MasterImagePath)).ReturnsAsync(true);
            _mockImageProcessingService.Setup(ips => ips.LoadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                      .ReturnsAsync(new LoadedImage());

            // Act
            var result = await service.CreateHueMosaicAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.MosaicId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task HueMosaicService_CreateColorMosaicAsync_ThrowsNotSupportedException()
        {
            // Arrange
            var service = new HueMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockHueLogger.Object);
            var request = new ColorMosaicRequest 
            { 
                MasterImagePath = "test.jpg", 
                SeedImagesDirectory = "seeds" 
            };

            // Act & Assert
            await FluentActions.Invoking(async () => await service.CreateColorMosaicAsync(request))
                               .Should().ThrowAsync<NotSupportedException>()
                               .WithMessage("Use ColorMosaicService for color-based mosaics");
        }

        #endregion

        #region PhotoMosaicService Tests

        [Fact]
        public void PhotoMosaicService_Constructor_ValidParameters_CreatesInstance()
        {
            // Act
            var service = new PhotoMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockPhotoLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IPhotoMosaicService>();
            service.Should().BeAssignableTo<IMosaicService>();
        }

        [Fact]
        public async Task PhotoMosaicService_CreatePhotoMosaicAsync_ValidRequest_ReturnsResult()
        {
            // Arrange
            var service = new PhotoMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockPhotoLogger.Object);
            var request = CreateValidPhotoMosaicRequest();

            // Setup mocks
            _mockFileOperations.Setup(fo => fo.IsValidImageFileAsync(request.MasterImagePath)).ReturnsAsync(true);
            _mockImageProcessingService.Setup(ips => ips.LoadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                      .ReturnsAsync(new LoadedImage());

            // Act
            var result = await service.CreatePhotoMosaicAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.MosaicId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task PhotoMosaicService_CreateColorMosaicAsync_ThrowsNotSupportedException()
        {
            // Arrange
            var service = new PhotoMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockPhotoLogger.Object);
            var request = new ColorMosaicRequest 
            { 
                MasterImagePath = "test.jpg", 
                SeedImagesDirectory = "seeds" 
            };

            // Act & Assert
            await FluentActions.Invoking(async () => await service.CreateColorMosaicAsync(request))
                               .Should().ThrowAsync<NotSupportedException>()
                               .WithMessage("Use ColorMosaicService for color-based mosaics");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task ColorMosaicService_NullRequest_ThrowsArgumentException()
        {
            // Arrange
            var service = new ColorMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockColorLogger.Object);

            // Act & Assert
            await FluentActions.Invoking(async () => await service.CreateColorMosaicAsync(null!))
                               .Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task HueMosaicService_InvalidImageFile_ReturnsFailedResult()
        {
            // Arrange
            var service = new HueMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockHueLogger.Object);
            var request = CreateValidHueMosaicRequest();

            // Setup mock to return invalid file
            _mockFileOperations.Setup(fo => fo.IsValidImageFileAsync(request.MasterImagePath)).ReturnsAsync(false);

            // Act
            var result = await service.CreateHueMosaicAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(MosaicStatus.Failed);
        }

        [Fact]
        public async Task PhotoMosaicService_ExceptionDuringProcessing_ReturnsFailedResult()
        {
            // Arrange
            var service = new PhotoMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockPhotoLogger.Object);
            var request = CreateValidPhotoMosaicRequest();

            // Setup mock to throw exception
            _mockFileOperations.Setup(fo => fo.IsValidImageFileAsync(request.MasterImagePath)).ReturnsAsync(true);
            _mockImageProcessingService.Setup(ips => ips.LoadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                      .ThrowsAsync(new IOException("File access error"));

            // Act
            var result = await service.CreatePhotoMosaicAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(MosaicStatus.Failed);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Service Integration Tests

        [Fact]
        public async Task AllServices_CancellationToken_RespectsTokenParameter()
        {
            // Arrange
            var colorService = new ColorMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockColorLogger.Object);
            var hueService = new HueMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockHueLogger.Object);
            var photoService = new PhotoMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockPhotoLogger.Object);

            var cancellationTokenSource = new CancellationTokenSource();
            
            // Setup mocks to simulate file operations that would check cancellation
            _mockFileOperations.Setup(fo => fo.IsValidImageFileAsync(It.IsAny<string>()))
                               .Returns(Task.FromResult(true));
            _mockImageProcessingService.Setup(ips => ips.LoadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                       .ThrowsAsync(new OperationCanceledException());

            // Act & Assert - Check that services handle cancellation tokens
            var colorResult = await colorService.CreateColorMosaicAsync(CreateValidColorMosaicRequest(), progressReporter: null, cancellationToken: cancellationTokenSource.Token);
            colorResult.Should().NotBeNull();
            colorResult.Status.Should().Be(MosaicStatus.Cancelled); // Should be cancelled due to cancellation token

            var hueResult = await hueService.CreateHueMosaicAsync(CreateValidHueMosaicRequest(), progressReporter: null, cancellationToken: cancellationTokenSource.Token);
            hueResult.Should().NotBeNull();
            hueResult.Status.Should().Be(MosaicStatus.Cancelled); // Should be cancelled due to cancellation token
        }

        [Fact]
        public void AllServices_InterfaceCompliance_ImplementCorrectInterfaces()
        {
            // Arrange & Act
            var colorService = new ColorMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockColorLogger.Object);
            var hueService = new HueMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockHueLogger.Object);
            var photoService = new PhotoMosaicService(_mockImageProcessingService.Object, _mockFileOperations.Object, _mockPhotoLogger.Object);

            // Assert - All services implement IMosaicService
            colorService.Should().BeAssignableTo<IMosaicService>();
            hueService.Should().BeAssignableTo<IMosaicService>();
            photoService.Should().BeAssignableTo<IMosaicService>();

            // Assert - Each service implements its specific interface
            colorService.Should().BeAssignableTo<IColorMosaicService>();
            hueService.Should().BeAssignableTo<IHueMosaicService>();
            photoService.Should().BeAssignableTo<IPhotoMosaicService>();
        }

        #endregion

        #region Helper Methods

        private ColorMosaicRequest CreateValidColorMosaicRequest()
        {
            return new ColorMosaicRequest
            {
                MasterImagePath = Path.Combine(_testDirectory, "master.jpg"),
                SeedImagesDirectory = Path.Combine(_testDirectory, "seeds"),
                OutputPath = Path.Combine(_testDirectory, "output.jpg"),
                PixelSize = 16,
                Quality = 90,
                ColorTolerance = 30
            };
        }

        private HueMosaicRequest CreateValidHueMosaicRequest()
        {
            return new HueMosaicRequest
            {
                MasterImagePath = Path.Combine(_testDirectory, "master.jpg"),
                SeedImagesDirectory = Path.Combine(_testDirectory, "seeds"),
                OutputPath = Path.Combine(_testDirectory, "output.jpg"),
                PixelSize = 16,
                Quality = 90,
                HueTolerance = 0.3,
                SaturationWeight = 60,
                BrightnessWeight = 40
            };
        }

        private PhotoMosaicRequest CreateValidPhotoMosaicRequest()
        {
            return new PhotoMosaicRequest
            {
                MasterImagePath = Path.Combine(_testDirectory, "master.jpg"),
                SeedImagesDirectory = Path.Combine(_testDirectory, "seeds"),
                OutputPath = Path.Combine(_testDirectory, "output.jpg"),
                PixelSize = 16,
                Quality = 90,
                SimilarityThreshold = 80,
                AvoidImageRepetition = true,
                MaxImageReuse = 3
            };
        }

        #endregion

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}