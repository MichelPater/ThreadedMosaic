using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class ColorMosaicTests : IDisposable
    {
        private readonly string _testImagePath;
        private readonly string _outputPath;

        public ColorMosaicTests()
        {
            _testImagePath = Path.GetTempFileName();
            _outputPath = Path.GetTempFileName();
            CreateTestImage(_testImagePath);
        }

        public void Dispose()
        {
            if (File.Exists(_testImagePath))
                File.Delete(_testImagePath);
            if (File.Exists(_outputPath))
                File.Delete(_outputPath);
        }

        private void CreateTestImage(string path)
        {
            using (var bitmap = new Bitmap(40, 40))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Blue);
                }
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        [Fact]
        public void Constructor_Should_Initialize_With_Valid_Parameters()
        {
            // Arrange & Act
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath);

            // Assert
            colorMosaic.Should().NotBeNull();
            colorMosaic.XPixelCount.Should().Be(40);
            colorMosaic.YPixelCount.Should().Be(40);
        }

        [Fact]
        public void Constructor_With_Invalid_MasterFile_Should_Throw_Exception()
        {
            // Arrange
            var invalidPath = @"C:\nonexistent\file.jpg";

            // Act & Assert
            Action act = () => new ColorMosaic(invalidPath, _outputPath);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_With_Null_Parameters_Should_Throw_Exception()
        {
            // Arrange, Act & Assert
            Action actWithNullMaster = () => new ColorMosaic(null, _outputPath);
            actWithNullMaster.Should().Throw<ArgumentNullException>();

            Action actWithNullOutput = () => new ColorMosaic(_testImagePath, null);
            actWithNullOutput.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CreateColorMosaic_Should_Complete_Without_Exception()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath);

            // Act & Assert
            Action act = () => colorMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(20, 30)]
        [InlineData(5, 5)]
        public void SetPixelSize_Should_Update_Tile_Dimensions(int width, int height)
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath);

            // Act
            colorMosaic.SetPixelSize(width, height);

            // Assert
            colorMosaic.XPixelCount.Should().Be(width);
            colorMosaic.YPixelCount.Should().Be(height);
        }

        [Fact]
        public void CreateColorMosaic_With_Small_Tile_Size_Should_Work()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(5, 5);

            // Act & Assert
            Action act = () => colorMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreateColorMosaic_With_Large_Tile_Size_Should_Work()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(80, 80);

            // Act & Assert
            Action act = () => colorMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(100, 100)]
        public void SetPixelSize_With_Edge_Cases_Should_Work(int width, int height)
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath);

            // Act
            colorMosaic.SetPixelSize(width, height);

            // Assert
            colorMosaic.XPixelCount.Should().Be(width);
            colorMosaic.YPixelCount.Should().Be(height);
        }

        [Fact]
        public void Multiple_CreateColorMosaic_Calls_Should_Work()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath);

            // Act & Assert
            Action firstCall = () => colorMosaic.CreateColorMosaic();
            firstCall.Should().NotThrow();

            Action secondCall = () => colorMosaic.CreateColorMosaic();
            secondCall.Should().NotThrow();
        }
    }
}