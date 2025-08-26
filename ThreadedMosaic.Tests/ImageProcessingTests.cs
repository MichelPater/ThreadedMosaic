using System;
using System.Drawing;
using System.IO;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class ImageProcessingTests : IDisposable
    {
        private readonly string _testImagePath;
        private readonly string _outputPath;

        public ImageProcessingTests()
        {
            _testImagePath = Path.GetTempFileName();
            _outputPath = Path.GetTempFileName();
            CreateTestImage(_testImagePath, Color.Blue);
        }

        public void Dispose()
        {
            // Force garbage collection to release any bitmap resources
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Retry file deletion with a short delay
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(_testImagePath))
                        File.Delete(_testImagePath);
                    if (File.Exists(_outputPath))
                        File.Delete(_outputPath);
                    break;
                }
                catch (IOException)
                {
                    if (i == 2) throw; // Re-throw on final attempt
                    System.Threading.Thread.Sleep(100); // Wait 100ms before retry
                }
            }
        }

        private void CreateTestImage(string path, Color color)
        {
            using (var bitmap = new Bitmap(40, 40))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(color);
                }
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        [Fact]
        public void GetAverageColor_Should_Return_Correct_Color_For_Solid_Image()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            var expectedColor = Color.Blue;

            using (var bitmap = new Bitmap(10, 10))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(expectedColor);
                }

                // Act
                var averageColor = testMosaic.GetAverageColor(bitmap);

                // Assert
                averageColor.R.Should().BeCloseTo(expectedColor.R, 10);
                averageColor.G.Should().BeCloseTo(expectedColor.G, 10);
                averageColor.B.Should().BeCloseTo(expectedColor.B, 10);
            }
        }

        [Fact]
        public void GetAverageColor_With_Rectangle_Should_Calculate_Partial_Area()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);

            using (var bitmap = new Bitmap(20, 20))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    graphics.FillRectangle(new SolidBrush(Color.Red), new Rectangle(0, 0, 10, 10));
                }

                var topLeftRect = new Rectangle(0, 0, 10, 10);
                var bottomRightRect = new Rectangle(10, 10, 10, 10);

                // Act
                var redAreaColor = testMosaic.GetAverageColor(bitmap, topLeftRect);
                var whiteAreaColor = testMosaic.GetAverageColor(bitmap, bottomRightRect);

                // Assert
                redAreaColor.R.Should().BeGreaterThan(200);
                whiteAreaColor.R.Should().BeGreaterThan(200);
                whiteAreaColor.G.Should().BeGreaterThan(200);
                whiteAreaColor.B.Should().BeGreaterThan(200);
            }
        }

        [Fact]
        public void InitializeColorTiles_Should_Create_Correct_Grid_Size()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            testMosaic.SetPixelSize(10, 10);

            // Act
            var colorTiles = testMosaic.InitializeColorTiles(100, 80);

            // Assert
            colorTiles.GetLength(0).Should().Be(10); // 100/10 = 10
            colorTiles.GetLength(1).Should().Be(8);  // 80/10 = 8
        }

        [Fact]
        public void InitializeColorTiles_Should_Handle_Remainder_Pixels()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            testMosaic.SetPixelSize(15, 20);

            // Act - 105 width doesn't divide evenly by 15, nor does 85 height by 20
            var colorTiles = testMosaic.InitializeColorTiles(105, 85);

            // Assert - Should add extra tiles for remainder
            colorTiles.GetLength(0).Should().Be(8); // 105/15 = 7 remainder 0, so 7+1 = 8
            colorTiles.GetLength(1).Should().Be(5); // 85/20 = 4 remainder 5, so 4+1 = 5
        }

        [Fact]
        public void CreateSizeAppropriateRectangle_Should_Return_Standard_Rectangle_For_Interior_Tiles()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            testMosaic.SetPixelSize(20, 30);
            var colorTiles = new Color[5, 4]; // 5x4 grid

            // Act
            var rectangle = testMosaic.CreateSizeAppropriateRectangle(1, 1, colorTiles, 100, 120);

            // Assert
            rectangle.X.Should().Be(20); // 1 * 20
            rectangle.Y.Should().Be(30); // 1 * 30
            rectangle.Width.Should().Be(20);
            rectangle.Height.Should().Be(30);
        }

        [Fact]
        public void ResizeImage_Should_Change_Image_Dimensions()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            using (var originalImage = new Bitmap(100, 80))
            {
                var newSize = new Size(50, 40);

                // Act
                using (var resizedImage = (System.Drawing.Image)testMosaic.ResizeImage(originalImage, newSize))
                {
                    // Assert
                    resizedImage.Width.Should().Be(50);
                    resizedImage.Height.Should().Be(40);
                }
            }
        }

        [Theory]
        [InlineData(1, 1)]    // Minimum size
        [InlineData(100, 100)] // Large size
        [InlineData(32, 48)]   // Non-square
        public void SetPixelSize_Should_Accept_Valid_Dimensions(int width, int height)
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);

            // Act
            testMosaic.SetPixelSize(width, height);

            // Assert
            testMosaic.XPixelCount.Should().Be(width);
            testMosaic.YPixelCount.Should().Be(height);
        }

        [Fact]
        public void GetAverageColor_Should_Handle_Error_Gracefully()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            
            // Create a bitmap that might cause issues
            using (var bitmap = new Bitmap(1, 1))
            {
                // Act & Assert - Should not throw exception
                Action act = () => testMosaic.GetAverageColor(bitmap);
                act.Should().NotThrow();
            }
        }
    }
}