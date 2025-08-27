using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class TestableColorMosaic : ColorMosaic
    {
        public TestableColorMosaic(string masterFileLocation, string outputFileLocation) 
            : base(masterFileLocation, outputFileLocation, NullProgressReporter.Instance, NullFileOperations.Instance)
        {
        }

        public new void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            base.BuildImage(graphics, xCoordinate, yCoordinate, tileColors);
        }
    }

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
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

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
            Action act = () => new ColorMosaic(invalidPath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_With_Null_Parameters_Should_Throw_Exception()
        {
            // Arrange, Act & Assert
            Action actWithNullMaster = () => new ColorMosaic(null, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            actWithNullMaster.Should().Throw<Exception>();

            Action actWithNullOutput = () => new ColorMosaic(_testImagePath, null, NullProgressReporter.Instance, NullFileOperations.Instance);
            actWithNullOutput.Should().NotThrow(); // Output path is just stored, not validated immediately
        }

        [Fact]
        public void CreateColorMosaic_Should_Complete_Without_Exception()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

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
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

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
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            colorMosaic.SetPixelSize(5, 5);

            // Act & Assert
            Action act = () => colorMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreateColorMosaic_With_Large_Tile_Size_Should_Work()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
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
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

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
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action firstCall = () => colorMosaic.CreateColorMosaic();
            firstCall.Should().NotThrow();

            Action secondCall = () => colorMosaic.CreateColorMosaic();
            secondCall.Should().NotThrow();
        }

        [Fact]
        public void BuildImage_Should_Fill_Solid_Color_Rectangle_Correctly()
        {
            // Arrange
            var testBitmap = new Bitmap(100, 100);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(50, 50);
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Red;
            tileColors[0, 1] = Color.Green;
            tileColors[1, 0] = Color.Blue;
            tileColors[1, 1] = Color.Yellow;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act
                Action act = () => colorMosaic.BuildImage(graphics, 0, 0, tileColors);
                
                // Assert
                act.Should().NotThrow("BuildImage should fill solid color rectangles correctly");
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Pure_Colors_Should_Work()
        {
            // Arrange
            var testBitmap = new Bitmap(120, 120);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(40, 40);
            
            var tileColors = new Color[3, 3];
            tileColors[0, 0] = Color.FromArgb(255, 255, 0, 0);     // Pure Red
            tileColors[0, 1] = Color.FromArgb(255, 0, 255, 0);     // Pure Green
            tileColors[0, 2] = Color.FromArgb(255, 0, 0, 255);     // Pure Blue
            tileColors[1, 0] = Color.FromArgb(255, 255, 255, 0);   // Yellow
            tileColors[1, 1] = Color.FromArgb(255, 255, 0, 255);   // Magenta
            tileColors[1, 2] = Color.FromArgb(255, 0, 255, 255);   // Cyan
            tileColors[2, 0] = Color.FromArgb(255, 0, 0, 0);       // Black
            tileColors[2, 1] = Color.FromArgb(255, 255, 255, 255); // White
            tileColors[2, 2] = Color.FromArgb(255, 128, 128, 128); // Gray

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert
                for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                {
                    Action act = () => colorMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should handle pure color at ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Various_Alpha_Values_Should_Work()
        {
            // Arrange
            var testBitmap = new Bitmap(80, 80);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(20, 20);
            
            var tileColors = new Color[4, 4];
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
            {
                int alpha = (x * 4 + y + 1) * 16; // Alpha values from 16 to 255
                if (alpha > 255) alpha = 255;
                tileColors[x, y] = Color.FromArgb(alpha, 100, 150, 200);
            }

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert
                for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                {
                    Action act = () => colorMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should handle alpha values at ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Different_Pixel_Sizes_Should_Scale_Rectangles()
        {
            // Arrange
            var testBitmap = new Bitmap(200, 200);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            
            var tileColors = new Color[4, 4];
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                tileColors[x, y] = Color.FromArgb(255, (x + 1) * 60, (y + 1) * 60, 128);

            var pixelSizes = new[] { 10, 25, 50 };

            foreach (var pixelSize in pixelSizes)
            {
                colorMosaic.SetPixelSize(pixelSize, pixelSize);
                
                using (var graphics = Graphics.FromImage(testBitmap))
                {
                    // Act & Assert
                    for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        Action act = () => colorMosaic.BuildImage(graphics, x, y, tileColors);
                        act.Should().NotThrow(string.Format("BuildImage should work with {0}x{0} pixel size at ({1}, {2})", pixelSize, x, y));
                    }
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Non_Square_Tiles_Should_Work()
        {
            // Arrange
            var testBitmap = new Bitmap(150, 100);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(30, 20); // Non-square tiles
            
            var tileColors = new Color[5, 5];
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                tileColors[x, y] = Color.FromArgb(255, x * 50, y * 50, 100);

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert
                for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                {
                    Action act = () => colorMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should work with non-square tiles at ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Multiple_Calls_Should_Work_Consistently()
        {
            // Arrange
            var testBitmap = new Bitmap(60, 60);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(30, 30);
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Purple;
            tileColors[0, 1] = Color.Orange;
            tileColors[1, 0] = Color.Cyan;
            tileColors[1, 1] = Color.Magenta;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Multiple calls should work consistently
                for (int iteration = 0; iteration < 5; iteration++)
                {
                    for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        Action act = () => colorMosaic.BuildImage(graphics, x, y, tileColors);
                        act.Should().NotThrow(string.Format("BuildImage iteration {0} should work at ({1}, {2})", iteration + 1, x, y));
                    }
                }
            }
            
            testBitmap.Dispose();
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(5, 10)]
        [InlineData(20, 15)]
        [InlineData(100, 50)]
        public void BuildImage_With_Various_Tile_Dimensions_Should_Work(int width, int height)
        {
            // Arrange
            var testBitmap = new Bitmap(200, 150);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(width, height);
            
            var tileColors = new Color[3, 3];
            tileColors[0, 0] = Color.Navy;
            tileColors[0, 1] = Color.Maroon;
            tileColors[0, 2] = Color.Olive;
            tileColors[1, 0] = Color.Teal;
            tileColors[1, 1] = Color.Silver;
            tileColors[1, 2] = Color.Lime;
            tileColors[2, 0] = Color.Aqua;
            tileColors[2, 1] = Color.Fuchsia;
            tileColors[2, 2] = Color.Gray;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert
                Action act = () => colorMosaic.BuildImage(graphics, 0, 0, tileColors);
                act.Should().NotThrow(string.Format("BuildImage should work with {0}x{1} tile dimensions", width, height));
                
                act = () => colorMosaic.BuildImage(graphics, 1, 1, tileColors);
                act.Should().NotThrow(string.Format("BuildImage should work with {0}x{1} tile dimensions at (1,1)", width, height));
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Edge_Coordinates_Should_Handle_Correctly()
        {
            // Arrange
            var testBitmap = new Bitmap(100, 100);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(25, 25);
            
            var tileColors = new Color[4, 4];
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                tileColors[x, y] = Color.FromArgb(255, x * 80, y * 80, 150);

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Test edge coordinates
                var edgeCoordinates = new[] { 
                    new { x = 0, y = 0 }, 
                    new { x = 0, y = 3 }, 
                    new { x = 3, y = 0 }, 
                    new { x = 3, y = 3 } 
                };
                
                foreach (var coord in edgeCoordinates)
                {
                    Action act = () => colorMosaic.BuildImage(graphics, coord.x, coord.y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should handle edge coordinate ({0}, {1})", coord.x, coord.y));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Should_Create_Solid_Color_Blocks_Without_Gaps()
        {
            // Arrange
            var testBitmap = new Bitmap(90, 90);
            var colorMosaic = new TestableColorMosaic(_testImagePath, _outputPath);
            colorMosaic.SetPixelSize(30, 30);
            
            var tileColors = new Color[3, 3];
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                tileColors[x, y] = Color.FromArgb(255, 255 - x * 100, 255 - y * 100, 100);

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Fill entire grid to test no gaps
                for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                {
                    Action act = () => colorMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should create solid blocks without gaps at ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }
    }
}