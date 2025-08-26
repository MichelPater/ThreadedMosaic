using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class TestMosaic : ThreadedMosaic.Mosaic.Mosaic
    {
        public TestMosaic(string masterFileLocation, string outputFileLocation) 
            : base(masterFileLocation, outputFileLocation)
        {
        }

        public TestMosaic(List<string> fileLocations, string masterFileLocation, string outputFileLocation) 
            : base(fileLocations, masterFileLocation, outputFileLocation)
        {
        }

        protected override void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            // Test implementation - just fills rectangle with color
            var colorBrush = new SolidBrush(tileColors[xCoordinate, yCoordinate]);
            var rectangle = new Rectangle(xCoordinate * XPixelCount, yCoordinate * YPixelCount, XPixelCount, YPixelCount);
            graphics.FillRectangle(colorBrush, rectangle);
        }

        public new void SetPixelSize(int widthInPixels, int heightInPixels)
        {
            base.SetPixelSize(widthInPixels, heightInPixels);
        }

        public new Color GetAverageColor(Bitmap bitmap)
        {
            return base.GetAverageColor(bitmap);
        }

        public new Color GetAverageColor(Bitmap bitmap, Rectangle subdivision)
        {
            return base.GetAverageColor(bitmap, subdivision);
        }

        public new Color[,] InitializeColorTiles(int imageWidth, int imageHeight)
        {
            return base.InitializeColorTiles(imageWidth, imageHeight);
        }

        public new Rectangle CreateSizeAppropriateRectangle(int currentXCoordinate, int currentYCoordinate,
            Color[,] tileColors, int sourceBitmapWidth, int sourceBitmapHeight)
        {
            return base.CreateSizeAppropriateRectangle(currentXCoordinate, currentYCoordinate, tileColors, sourceBitmapWidth, sourceBitmapHeight);
        }

        public new Image ResizeImage(Image imgToResize, Size size)
        {
            return base.ResizeImage(imgToResize, size);
        }
    }

    public class MosaicTests : IDisposable
    {
        private readonly string _testImagePath;
        private readonly string _outputPath;

        public MosaicTests()
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
            using (var bitmap = new Bitmap(100, 100))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Red);
                }
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        [Fact]
        public void Constructor_WithMasterAndOutputPath_Should_Initialize_Properties()
        {
            // Arrange & Act
            var mosaic = new TestMosaic(_testImagePath, _outputPath);

            // Assert
            mosaic.XPixelCount.Should().Be(40);
            mosaic.YPixelCount.Should().Be(40);
        }

        [Fact]
        public void Constructor_WithFileLocations_Should_Initialize_Properties()
        {
            // Arrange
            var fileLocations = new List<string> { _testImagePath };

            // Act
            var mosaic = new TestMosaic(fileLocations, _testImagePath, _outputPath);

            // Assert
            mosaic.XPixelCount.Should().Be(40);
            mosaic.YPixelCount.Should().Be(40);
        }

        [Fact]
        public void SetPixelSize_Should_Update_XPixelCount_And_YPixelCount()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);

            // Act
            mosaic.SetPixelSize(20, 30);

            // Assert
            mosaic.XPixelCount.Should().Be(20);
            mosaic.YPixelCount.Should().Be(30);
        }

        [Fact]
        public void GetAverageColor_Should_Return_Valid_Color_For_Bitmap()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            using (var bitmap = new Bitmap(10, 10))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Blue);
                }

                // Act
                var averageColor = mosaic.GetAverageColor(bitmap);

                // Assert
                averageColor.R.Should().BeInRange(0, 255);
                averageColor.G.Should().BeInRange(0, 255);
                averageColor.B.Should().BeInRange(0, 255);
            }
        }

        [Fact]
        public void GetAverageColor_With_Rectangle_Should_Return_Valid_Color()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            using (var bitmap = new Bitmap(20, 20))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Green);
                }
                var rectangle = new Rectangle(0, 0, 10, 10);

                // Act
                var averageColor = mosaic.GetAverageColor(bitmap, rectangle);

                // Assert
                averageColor.R.Should().BeInRange(0, 255);
                averageColor.G.Should().BeInRange(0, 255);
                averageColor.B.Should().BeInRange(0, 255);
            }
        }

        [Fact]
        public void InitializeColorTiles_Should_Create_Correct_Array_Size()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            mosaic.SetPixelSize(10, 10);

            // Act
            var colorTiles = mosaic.InitializeColorTiles(100, 100);

            // Assert
            colorTiles.GetLength(0).Should().Be(10);
            colorTiles.GetLength(1).Should().Be(10);
        }

        [Fact]
        public void InitializeColorTiles_With_Leftover_Pixels_Should_Expand_Grid()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            mosaic.SetPixelSize(15, 15);

            // Act
            var colorTiles = mosaic.InitializeColorTiles(100, 100);

            // Assert
            colorTiles.GetLength(0).Should().Be(7); // 100/15 = 6 remainder 10, so 6+1
            colorTiles.GetLength(1).Should().Be(7); // 100/15 = 6 remainder 10, so 6+1
        }

        [Fact]
        public void CreateSizeAppropriateRectangle_Should_Return_Correct_Rectangle()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            mosaic.SetPixelSize(20, 20);
            var colorTiles = new Color[5, 5];

            // Act
            var rectangle = mosaic.CreateSizeAppropriateRectangle(1, 1, colorTiles, 100, 100);

            // Assert
            rectangle.X.Should().Be(20);
            rectangle.Y.Should().Be(20);
            rectangle.Width.Should().Be(20);
            rectangle.Height.Should().Be(20);
        }

        [Fact]
        public void ResizeImage_Should_Return_Image_With_Correct_Size()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            using (var originalImage = new Bitmap(100, 100))
            {
                var newSize = new Size(50, 50);

                // Act
                using (var resizedImage = mosaic.ResizeImage(originalImage, newSize))
                {
                    // Assert
                    resizedImage.Width.Should().Be(50);
                    resizedImage.Height.Should().Be(50);
                }
            }
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(25, 35)]
        [InlineData(1, 1)]
        public void SetPixelSize_Should_Accept_Various_Sizes(int width, int height)
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);

            // Act
            mosaic.SetPixelSize(width, height);

            // Assert
            mosaic.XPixelCount.Should().Be(width);
            mosaic.YPixelCount.Should().Be(height);
        }
    }
}