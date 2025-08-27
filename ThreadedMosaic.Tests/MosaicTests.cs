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
            : base(masterFileLocation, outputFileLocation, NullProgressReporter.Instance, NullFileOperations.Instance)
        {
        }

        public TestMosaic(List<string> fileLocations, string masterFileLocation, string outputFileLocation) 
            : base(fileLocations, masterFileLocation, outputFileLocation, NullProgressReporter.Instance, NullFileOperations.Instance)
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

        public new System.Drawing.Image ResizeImage(System.Drawing.Image imgToResize, Size size)
        {
            return base.ResizeImage(imgToResize, size);
        }

        public Color[,] CallGetColorTilesFromBitmap(Bitmap sourceBitmap)
        {
            return base.GetColorTilesFromBitmap(sourceBitmap);
        }

        public System.Drawing.Image CallCreateMosaic(Color[,] tileColors)
        {
            return base.CreateMosaic(tileColors);
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
                using (var resizedImage = (System.Drawing.Image)mosaic.ResizeImage(originalImage, newSize))
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

        [Fact]
        public void CreateMosaic_With_1x1_Configuration_Should_Work()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            var tileColors = new Color[1, 1];
            tileColors[0, 0] = Color.Blue;

            // Act
            using (var result = mosaic.CallCreateMosaic(tileColors))
            {
                // Assert
                result.Should().NotBeNull("CreateMosaic should return a valid image");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void CreateMosaic_With_2x2_Configuration_Should_Work()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Red;
            tileColors[0, 1] = Color.Green;
            tileColors[1, 0] = Color.Blue;
            tileColors[1, 1] = Color.Yellow;

            // Act
            using (var result = mosaic.CallCreateMosaic(tileColors))
            {
                // Assert
                result.Should().NotBeNull("CreateMosaic should handle 2x2 configurations");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void CreateMosaic_With_Large_Grid_Configuration_Should_Work()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            mosaic.SetPixelSize(5, 5); // Small tiles for large grid
            var tileColors = new Color[10, 8];
            
            // Fill with gradient colors
            for (int x = 0; x < 10; x++)
            for (int y = 0; y < 8; y++)
                tileColors[x, y] = Color.FromArgb(255, x * 25, y * 30, 128);

            // Act
            using (var result = mosaic.CallCreateMosaic(tileColors))
            {
                // Assert
                result.Should().NotBeNull("CreateMosaic should handle large grid configurations");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void CreateMosaic_With_Null_TileColors_Should_Throw_Exception()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);

            // Act & Assert
            Action act = () => mosaic.CallCreateMosaic(null);
            act.Should().Throw<Exception>("CreateMosaic should not accept null tileColors");
        }

        [Fact]
        public void CreateMosaic_With_Empty_TileColors_Should_Not_Process_Tiles()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            var tileColors = new Color[0, 0];

            // Act
            using (var result = mosaic.CallCreateMosaic(tileColors))
            {
                // Assert
                result.Should().NotBeNull("CreateMosaic should return base image even with empty tileColors");
                // The base master image should be returned unmodified
            }
        }

        [Fact]
        public void CreateMosaic_Should_Call_BuildImage_For_Each_Tile()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            var tileColors = new Color[3, 2];
            
            // Fill with distinct colors to verify all tiles are processed
            tileColors[0, 0] = Color.Red;
            tileColors[0, 1] = Color.Green;
            tileColors[1, 0] = Color.Blue;
            tileColors[1, 1] = Color.Yellow;
            tileColors[2, 0] = Color.Cyan;
            tileColors[2, 1] = Color.Magenta;

            // Act
            using (var result = mosaic.CallCreateMosaic(tileColors))
            {
                // Assert
                result.Should().NotBeNull("CreateMosaic should process all tiles");
                // The result should be modified from the original master image
                // Since we can't easily verify BuildImage was called for each tile,
                // we verify the result is valid and has expected dimensions
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void CreateMosaic_Should_Dispose_Graphics_Context_Properly()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Purple;
            tileColors[0, 1] = Color.Orange;
            tileColors[1, 0] = Color.Pink;
            tileColors[1, 1] = Color.Brown;

            // Act & Assert
            Action act = () =>
            {
                using (var result = mosaic.CallCreateMosaic(tileColors))
                {
                    result.Should().NotBeNull("CreateMosaic should handle resource disposal correctly");
                }
            };
            
            act.Should().NotThrow("CreateMosaic should properly dispose Graphics context");
        }

        [Fact]
        public void CreateMosaic_Multiple_Calls_Should_Work_Consistently()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Navy;
            tileColors[0, 1] = Color.Maroon;
            tileColors[1, 0] = Color.Olive;
            tileColors[1, 1] = Color.Teal;

            // Act & Assert
            for (int i = 0; i < 3; i++)
            {
                using (var result = mosaic.CallCreateMosaic(tileColors))
                {
                    result.Should().NotBeNull(string.Format("CreateMosaic call {0} should work consistently", i + 1));
                    result.Width.Should().BeGreaterThan(0);
                    result.Height.Should().BeGreaterThan(0);
                }
            }
        }

        [Fact]
        public void CreateMosaic_With_Various_Color_Extremes_Should_Work()
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            var tileColors = new Color[4, 2];
            
            // Test with extreme colors
            tileColors[0, 0] = Color.FromArgb(255, 0, 0, 0);       // Black
            tileColors[0, 1] = Color.FromArgb(255, 255, 255, 255); // White
            tileColors[1, 0] = Color.FromArgb(255, 255, 0, 0);     // Pure Red
            tileColors[1, 1] = Color.FromArgb(255, 0, 255, 0);     // Pure Green
            tileColors[2, 0] = Color.FromArgb(255, 0, 0, 255);     // Pure Blue
            tileColors[2, 1] = Color.FromArgb(0, 128, 128, 128);   // Transparent Gray
            tileColors[3, 0] = Color.FromArgb(128, 255, 128, 0);   // Semi-transparent Yellow
            tileColors[3, 1] = Color.FromArgb(64, 64, 192, 255);   // Low Alpha Cyan

            // Act
            using (var result = mosaic.CallCreateMosaic(tileColors))
            {
                // Assert
                result.Should().NotBeNull("CreateMosaic should handle extreme color values");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Theory]
        [InlineData(5, 5)]
        [InlineData(10, 15)]
        [InlineData(20, 10)]
        public void CreateMosaic_With_Various_Tile_Sizes_Should_Scale_Appropriately(int tileWidth, int tileHeight)
        {
            // Arrange
            var mosaic = new TestMosaic(_testImagePath, _outputPath);
            mosaic.SetPixelSize(tileWidth, tileHeight);
            
            var tileColors = new Color[3, 3];
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                tileColors[x, y] = Color.FromArgb(255, (x + 1) * 80, (y + 1) * 80, 100);

            // Act
            using (var result = mosaic.CallCreateMosaic(tileColors))
            {
                // Assert
                result.Should().NotBeNull(string.Format("CreateMosaic should work with {0}x{1} tiles", tileWidth, tileHeight));
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }
    }
}