using FluentAssertions;
using Moq;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.Services;
using ThreadedMosaic.Core.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading;

namespace ThreadedMosaic.Tests.Core
{
    /// <summary>
    /// Tests for ImageSharpProcessingService with correct interface matching
    /// </summary>
    public class ImageProcessingServiceTests : IDisposable
    {
        private readonly Mock<ILogger<ImageSharpProcessingService>> _mockLogger;
        private readonly ImageSharpProcessingService _service;
        private readonly string _testImagePath;
        private readonly string _testDirectory;

        public ImageProcessingServiceTests()
        {
            _mockLogger = new Mock<ILogger<ImageSharpProcessingService>>();
            
            // Use a real MemoryCache instead of mocking it
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            _service = new ImageSharpProcessingService(memoryCache, _mockLogger.Object);
            
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "ThreadedMosaicTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            
            // Create a simple test image
            _testImagePath = CreateTestImage();
        }

        [Fact]
        public async Task LoadImageAsync_ValidImagePath_ReturnsLoadedImage()
        {
            // Act
            var result = await _service.LoadImageAsync(_testImagePath);

            // Assert
            result.Should().NotBeNull();
            result.FilePath.Should().Be(_testImagePath);
            result.Width.Should().BeGreaterThan(0);
            result.Height.Should().BeGreaterThan(0);
            result.AverageColor.Should().NotBeNull();
        }

        [Fact]
        public async Task LoadImageAsync_InvalidPath_ThrowsFileNotFoundException()
        {
            // Arrange
            var invalidPath = Path.Combine(_testDirectory, "nonexistent.jpg");

            // Act & Assert
            await FluentActions.Invoking(() => _service.LoadImageAsync(invalidPath))
                .Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task IsValidImageFormatAsync_ValidImageFile_ReturnsTrue()
        {
            // Act
            var result = await _service.IsValidImageFormatAsync(_testImagePath);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsValidImageFormatAsync_InvalidFile_ReturnsFalse()
        {
            // Arrange
            var textFile = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(textFile, "This is not an image");

            // Act
            var result = await _service.IsValidImageFormatAsync(textFile);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetImageDimensionsAsync_ValidImage_ReturnsCorrectDimensions()
        {
            // Act
            var result = await _service.GetImageDimensionsAsync(_testImagePath);

            // Assert
            result.Width.Should().BeGreaterThan(0);
            result.Height.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ResizeImageAsync_ValidParameters_ResizesCorrectly()
        {
            // Arrange
            var originalImage = await _service.LoadImageAsync(_testImagePath);
            const int targetWidth = 100;
            const int targetHeight = 100;

            // Act
            var resizedImage = await _service.ResizeImageAsync(originalImage, targetWidth, targetHeight);

            // Assert
            resizedImage.Should().NotBeNull();
            resizedImage.Width.Should().Be(targetWidth);
            resizedImage.Height.Should().Be(targetHeight);
        }

        [Fact]
        public async Task CreateThumbnailAsync_ValidParameters_CreatesThumbnail()
        {
            // Act
            var thumbnailData = await _service.CreateThumbnailAsync(_testImagePath, 150, 150);

            // Assert
            thumbnailData.Should().NotBeNull();
            thumbnailData.Length.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(ImageFormat.Png)]
        [InlineData(ImageFormat.Jpeg)]
        [InlineData(ImageFormat.Bmp)]
        public async Task SaveImageAsync_DifferentFormats_SavesSuccessfully(ImageFormat format)
        {
            // Arrange
            var image = await _service.LoadImageAsync(_testImagePath);
            var outputPath = Path.Combine(_testDirectory, $"output.{format.ToString().ToLower()}");

            // Act
            await _service.SaveImageAsync(image, outputPath, format);

            // Assert
            File.Exists(outputPath).Should().BeTrue();
        }

        [Fact]
        public async Task AnalyzeImageAsync_ValidImage_ReturnsAnalysis()
        {
            // Arrange
            var image = await _service.LoadImageAsync(_testImagePath);

            // Act
            var analysis = await _service.AnalyzeImageAsync(image);

            // Assert
            analysis.Should().NotBeNull();
            analysis.AverageColor.Should().NotBeNull();
            analysis.DominantColor.Should().NotBeNull();
        }

        private string CreateTestImage()
        {
            var imagePath = Path.Combine(_testDirectory, "test.png");
            
            // Create a simple 50x50 image using ImageSharp
            using var image = new Image<Rgb24>(50, 50);
            
            // Fill with red color by setting each pixel
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    image[x, y] = new Rgb24(255, 0, 0); // Red
                }
            }
            
            image.Save(imagePath);
            return imagePath;
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