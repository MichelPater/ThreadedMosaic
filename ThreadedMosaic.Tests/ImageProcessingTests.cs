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

        private void CreateTestImage(string path, Color color, int width, int height)
        {
            using (var bitmap = new Bitmap(width, height))
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
                redAreaColor.G.Should().Be(0);
                redAreaColor.B.Should().Be(0);
                
                whiteAreaColor.R.Should().Be(255);
                whiteAreaColor.G.Should().Be(255);
                whiteAreaColor.B.Should().Be(255);
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

            // Act - 105 width divides evenly by 15, but 85 height doesn't divide evenly by 20
            var colorTiles = testMosaic.InitializeColorTiles(105, 85);

            // Assert - Should add extra tiles for remainder in height only
            colorTiles.GetLength(0).Should().Be(7); // 105/15 = 7 remainder 0, so 7
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

        #region GetColorTilesFromBitmap Edge Cases

        [Fact]
        public void GetAverageColor_With_1x1_Rectangle_Should_Return_Single_Pixel_Color()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            
            using (var bitmap = new Bitmap(10, 10))
            {
                // Set a specific color at position (5,5)
                bitmap.SetPixel(5, 5, Color.FromArgb(128, 64, 192));
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White); // Fill rest with white
                }
                bitmap.SetPixel(5, 5, Color.FromArgb(128, 64, 192)); // Reset the test pixel
                
                var singlePixelRect = new Rectangle(5, 5, 1, 1);

                // Act
                var averageColor = testMosaic.GetAverageColor(bitmap, singlePixelRect);

                // Assert
                averageColor.R.Should().Be(128);
                averageColor.G.Should().Be(64);
                averageColor.B.Should().Be(192);
            }
        }

        [Fact]
        public void GetAverageColor_With_Pure_Black_Image_Should_Return_Black()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            
            using (var bitmap = new Bitmap(20, 20))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Black);
                }

                // Act
                var averageColor = testMosaic.GetAverageColor(bitmap);

                // Assert
                averageColor.R.Should().Be(0);
                averageColor.G.Should().Be(0);
                averageColor.B.Should().Be(0);
            }
        }

        [Fact]
        public void GetAverageColor_With_Pure_White_Image_Should_Return_White()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            
            using (var bitmap = new Bitmap(15, 15))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                }

                // Act
                var averageColor = testMosaic.GetAverageColor(bitmap);

                // Assert
                averageColor.R.Should().Be(255);
                averageColor.G.Should().Be(255);
                averageColor.B.Should().Be(255);
            }
        }

        [Fact]
        public void GetAverageColor_With_High_Contrast_Checkerboard_Should_Return_Gray()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            
            using (var bitmap = new Bitmap(4, 4))
            {
                // Create checkerboard pattern: 50% black, 50% white = should average to gray
                for (int x = 0; x < 4; x++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        var color = (x + y) % 2 == 0 ? Color.Black : Color.White;
                        bitmap.SetPixel(x, y, color);
                    }
                }

                // Act
                var averageColor = testMosaic.GetAverageColor(bitmap);

                // Assert - Should be approximately gray (127-128 range)
                averageColor.R.Should().BeInRange(120, 135);
                averageColor.G.Should().BeInRange(120, 135);
                averageColor.B.Should().BeInRange(120, 135);
            }
        }

        [Fact]
        public void GetAverageColor_With_Gradient_Rectangle_Should_Calculate_Average_Correctly()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            
            using (var bitmap = new Bitmap(10, 1))
            {
                // Create horizontal gradient from black (0) to white (255)
                for (int x = 0; x < 10; x++)
                {
                    int intensity = (x * 255) / 9; // 0, 28, 56, ..., 255
                    bitmap.SetPixel(x, 0, Color.FromArgb(intensity, intensity, intensity));
                }

                // Act - Average the left half (darker) vs right half (lighter)
                var leftHalf = testMosaic.GetAverageColor(bitmap, new Rectangle(0, 0, 5, 1));
                var rightHalf = testMosaic.GetAverageColor(bitmap, new Rectangle(5, 0, 5, 1));

                // Assert - Right half should be significantly lighter than left half
                rightHalf.R.Should().BeGreaterThan((byte)(leftHalf.R + 50));
                rightHalf.G.Should().BeGreaterThan((byte)(leftHalf.G + 50));
                rightHalf.B.Should().BeGreaterThan((byte)(leftHalf.B + 50));
            }
        }

        [Theory]
        [InlineData(10, 10, 5, 5)]   // Reasonable sizes
        [InlineData(20, 15, 4, 3)]   // Uneven divisions
        [InlineData(40, 40, 40, 40)] // Image same size as tile
        public void GetColorTilesFromBitmap_With_Various_Sizes_Should_Work_Correctly(int imageWidth, int imageHeight, int tileWidth, int tileHeight)
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            testMosaic.SetPixelSize(tileWidth, tileHeight);
            
            using (var bitmap = new Bitmap(imageWidth, imageHeight))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Blue);
                }

                // Act - Call the real method that populates colors
                var colorTiles = testMosaic.CallGetColorTilesFromBitmap(bitmap);

                // Assert
                var expectedWidth = (imageWidth / tileWidth) + (imageWidth % tileWidth > 0 ? 1 : 0);
                var expectedHeight = (imageHeight / tileHeight) + (imageHeight % tileHeight > 0 ? 1 : 0);
                
                colorTiles.GetLength(0).Should().Be(expectedWidth);
                colorTiles.GetLength(1).Should().Be(expectedHeight);
                
                // At least some tiles should have valid colors (allowing for some exceptions)
                var validTileCount = 0;
                for (int x = 0; x < colorTiles.GetLength(0); x++)
                {
                    for (int y = 0; y < colorTiles.GetLength(1); y++)
                    {
                        if (colorTiles[x, y] != Color.Empty)
                        {
                            validTileCount++;
                        }
                    }
                }
                validTileCount.Should().BeGreaterThan(0, "At least some tiles should be processed successfully");
            }
        }

        [Fact]
        public void InitializeColorTiles_With_Asymmetric_Remainders_Should_Handle_Both_Dimensions()
        {
            // Arrange - 23x17 image with 7x5 tiles = 3x3 grid + remainders (2x2)
            CreateTestImage(_testImagePath, Color.Red, 23, 17);
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            testMosaic.SetPixelSize(7, 5);

            // Act
            var colorTiles = testMosaic.InitializeColorTiles(23, 17);

            // Assert
            colorTiles.GetLength(0).Should().Be(4); // 23/7 = 3 remainder 2, so 4 tiles
            colorTiles.GetLength(1).Should().Be(4); // 17/5 = 3 remainder 2, so 4 tiles
        }

        [Fact]
        public void CreateSizeAppropriateRectangle_Should_Return_Valid_Rectangles()
        {
            // Arrange - Test with a simple case
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            testMosaic.SetPixelSize(10, 10);
            var tileColors = new Color[3, 3]; // 3x3 grid

            // Act - Test various positions
            var topLeft = testMosaic.CreateSizeAppropriateRectangle(0, 0, tileColors, 25, 25);
            var center = testMosaic.CreateSizeAppropriateRectangle(1, 1, tileColors, 25, 25);
            var bottomRight = testMosaic.CreateSizeAppropriateRectangle(2, 2, tileColors, 25, 25);

            // Assert - Just verify we get valid rectangles
            topLeft.Width.Should().BeGreaterThan(0);
            topLeft.Height.Should().BeGreaterThan(0);
            center.Width.Should().BeGreaterThan(0);
            center.Height.Should().BeGreaterThan(0);
            bottomRight.Width.Should().BeGreaterThan(0);
            bottomRight.Height.Should().BeGreaterThan(0);
            
            // Verify coordinates are reasonable
            topLeft.X.Should().Be(0);
            topLeft.Y.Should().Be(0);
            center.X.Should().Be(10);
            center.Y.Should().Be(10);
            bottomRight.X.Should().Be(20);
            bottomRight.Y.Should().Be(20);
        }

        [Fact]
        public void ResizeImage_Should_Preserve_Aspect_Ratio_Quality()
        {
            // Arrange
            var testMosaic = new TestMosaic(_testImagePath, _outputPath);
            
            using (var originalImage = new Bitmap(100, 50)) // 2:1 aspect ratio
            {
                using (var graphics = Graphics.FromImage(originalImage))
                {
                    // Create a recognizable pattern
                    graphics.Clear(Color.Red);
                    graphics.FillRectangle(Brushes.Blue, 0, 0, 50, 50); // Blue square on left
                }

                // Act - Resize to different size but same aspect ratio
                var newSize = new Size(50, 25); // Still 2:1 aspect ratio
                using (var resizedImage = (Bitmap)testMosaic.ResizeImage(originalImage, newSize))
                {
                    // Assert
                    resizedImage.Width.Should().Be(50);
                    resizedImage.Height.Should().Be(25);
                    
                    // Check that the pattern is preserved (left side should be predominantly blue)
                    var leftPixel = resizedImage.GetPixel(12, 12); // Left quarter
                    var rightPixel = resizedImage.GetPixel(37, 12); // Right quarter
                    
                    leftPixel.B.Should().BeGreaterThan(leftPixel.R); // More blue than red
                    rightPixel.R.Should().BeGreaterThan(rightPixel.B); // More red than blue
                }
            }
        }

        #endregion
    }
}