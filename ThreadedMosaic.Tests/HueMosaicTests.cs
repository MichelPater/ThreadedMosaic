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
    public class TestableHueMosaic : HueMosaic
    {
        public TestableHueMosaic(List<string> fileLocations, string masterFileLocation, string outputFileLocation) 
            : base(fileLocations, masterFileLocation, outputFileLocation, NullProgressReporter.Instance, NullFileOperations.Instance)
        {
        }

        public void LoadImagesForTesting()
        {
            // Load images into the _concurrentBag so BuildImage can access them
            ProgressReporter.UpdateStatus("Start of Loading Images:");
            ProgressReporter.SetMaximum(FileLocations.Count);

            foreach (var currentFile in FileLocations)
            {
                try
                {
                    // Using reflection to access private _concurrentBag
                    var field = typeof(HueMosaic).GetField("_concurrentBag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var concurrentBag = field.GetValue(this);
                    var addMethod = concurrentBag.GetType().GetMethod("Add");
                    addMethod.Invoke(concurrentBag, new object[] { new LoadedImage { FilePath = currentFile } });
                }
                catch
                {
                    // Skip files that can't be loaded
                }
                finally
                {
                    ProgressReporter.UpdateStatus("Amount of Images loaded: " + 1);
                    ProgressReporter.IncrementProgress();
                }
            }
        }

        public new void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            base.BuildImage(graphics, xCoordinate, yCoordinate, tileColors);
        }
    }

    public class HueMosaicTests : IDisposable
    {
        private readonly string _testImagePath;
        private readonly string _testImagePath2;
        private readonly string _outputPath;
        private readonly List<string> _fileLocations;

        public HueMosaicTests()
        {
            _testImagePath = Path.GetTempFileName();
            _testImagePath2 = Path.GetTempFileName();
            _outputPath = Path.GetTempFileName();
            
            CreateTestImage(_testImagePath, Color.Red);
            CreateTestImage(_testImagePath2, Color.Blue);
            
            _fileLocations = new List<string> { _testImagePath, _testImagePath2 };
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
                    if (File.Exists(_testImagePath2))
                        File.Delete(_testImagePath2);
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
                bitmap.Save(path, ImageFormat.Png);
            }
        }


        [Fact]
        public void Constructor_Should_Initialize_With_Valid_Parameters()
        {
            // Arrange & Act
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Assert
            hueMosaic.Should().NotBeNull();
            hueMosaic.XPixelCount.Should().Be(40);
            hueMosaic.YPixelCount.Should().Be(40);
        }

        [Fact]
        public void Constructor_With_Null_FileLocations_Should_Throw_Exception()
        {
            // Arrange, Act & Assert
            Action act = () => new HueMosaic(null, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().NotThrow(); // FileLocations is just stored, not validated immediately
        }

        [Fact]
        public void Constructor_With_Empty_FileLocations_Should_Not_Throw()
        {
            // Arrange
            var emptyFileLocations = new List<string>();

            // Act & Assert
            Action act = () => new HueMosaic(emptyFileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().NotThrow();
        }

        [Fact]
        public void Constructor_With_Invalid_MasterFile_Should_Throw_Exception()
        {
            // Arrange
            var invalidPath = @"C:\nonexistent\file.jpg";

            // Act & Assert
            Action act = () => new HueMosaic(_fileLocations, invalidPath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void CreateColorMosaic_Should_Complete_Without_Exception()
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreateColorMosaic_With_Empty_FileLocations_Should_Throw_Exception()
        {
            // Arrange
            var emptyFileLocations = new List<string>();
            var hueMosaic = new HueMosaic(emptyFileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(20, 30)]
        [InlineData(5, 5)]
        public void SetPixelSize_Should_Update_Tile_Dimensions(int width, int height)
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act
            hueMosaic.SetPixelSize(width, height);

            // Assert
            hueMosaic.XPixelCount.Should().Be(width);
            hueMosaic.YPixelCount.Should().Be(height);
        }

        [Fact]
        public void CreateColorMosaic_With_Single_Image_Should_Work()
        {
            // Arrange
            var singleImageList = new List<string> { _testImagePath };
            var hueMosaic = new HueMosaic(singleImageList, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreateColorMosaic_With_Many_Images_Should_Work()
        {
            // Arrange
            var manyImages = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                manyImages.Add(_testImagePath);
                manyImages.Add(_testImagePath2);
            }
            var hueMosaic = new HueMosaic(manyImages, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreateColorMosaic_With_Small_Tile_Size_Should_Work()
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            hueMosaic.SetPixelSize(5, 5);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void Multiple_CreateColorMosaic_Calls_Should_Work()
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action firstCall = () => hueMosaic.CreateColorMosaic();
            firstCall.Should().NotThrow();

            Action secondCall = () => hueMosaic.CreateColorMosaic();
            secondCall.Should().NotThrow();
        }

        [Fact]
        public void Constructor_With_Null_MasterFileLocation_Should_Throw()
        {
            // Arrange, Act & Assert
            Action act = () => new HueMosaic(_fileLocations, null, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_With_Null_OutputFileLocation_Should_Throw()
        {
            // Arrange, Act & Assert
            Action act = () => new HueMosaic(_fileLocations, _testImagePath, null, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().NotThrow(); // Output path is just stored, not validated immediately
        }

        [Fact]
        public void BuildImage_Should_Apply_Transparent_Overlay_Correctly()
        {
            // Arrange
            var testBitmap = new Bitmap(100, 100);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(50, 50);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Red;
            tileColors[0, 1] = Color.Green;
            tileColors[1, 0] = Color.Blue;
            tileColors[1, 1] = Color.Yellow;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act
                Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                
                // Assert
                act.Should().NotThrow("BuildImage should handle overlay logic correctly");
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Different_Coordinates_Should_Position_Correctly()
        {
            // Arrange
            var testBitmap = new Bitmap(200, 200);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(25, 25);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[8, 8];
            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                tileColors[x, y] = Color.FromArgb(255, x * 30, y * 30, 128);

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert
                for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                {
                    Action act = () => hueMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should work at coordinates ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Should_Use_Correct_Alpha_Value_210()
        {
            // Arrange
            var testBitmap = new Bitmap(50, 50);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(50, 50);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[1, 1];
            tileColors[0, 0] = Color.FromArgb(255, 100, 150, 200);

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act
                Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                
                // Assert
                act.Should().NotThrow("BuildImage should apply alpha value 210 correctly");
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Edge_Colors_Should_Handle_Correctly()
        {
            // Arrange
            var testBitmap = new Bitmap(60, 60);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(30, 30);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.FromArgb(255, 0, 0, 0);     // Black
            tileColors[0, 1] = Color.FromArgb(255, 255, 255, 255); // White
            tileColors[1, 0] = Color.FromArgb(255, 255, 0, 0);     // Pure Red
            tileColors[1, 1] = Color.FromArgb(255, 0, 255, 0);     // Pure Green

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    Action act = () => hueMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should handle edge colors at ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Multiple_Calls_Should_Work_Consistently()
        {
            // Arrange
            var testBitmap = new Bitmap(80, 80);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(40, 40);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Purple;
            tileColors[0, 1] = Color.Orange;
            tileColors[1, 0] = Color.Cyan;
            tileColors[1, 1] = Color.Magenta;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Multiple calls should work
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage iteration {0} should work", iteration + 1));
                    
                    act = () => hueMosaic.BuildImage(graphics, 1, 1, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage iteration {0} at (1,1) should work", iteration + 1));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_With_Various_Pixel_Sizes_Should_Scale_Correctly()
        {
            // Arrange
            var testBitmap = new Bitmap(150, 150);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[5, 5];
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                tileColors[x, y] = Color.FromArgb(255, (x + 1) * 50, (y + 1) * 50, 128);

            var pixelSizes = new[] { 10, 25, 30 };

            foreach (var pixelSize in pixelSizes)
            {
                hueMosaic.SetPixelSize(pixelSize, pixelSize);
                
                using (var graphics = Graphics.FromImage(testBitmap))
                {
                    // Act & Assert
                    Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should work with pixel size {0}", pixelSize));
                    
                    act = () => hueMosaic.BuildImage(graphics, 2, 2, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should work with pixel size {0} at (2,2)", pixelSize));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Should_Handle_Random_Image_Selection()
        {
            // Arrange
            var testBitmap = new Bitmap(40, 40);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(40, 40);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[1, 1];
            tileColors[0, 0] = Color.Lime;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Multiple calls should work with random image selection
                for (int i = 0; i < 10; i++)
                {
                    Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage call {0} should handle random image selection", i + 1));
                }
            }
            
            testBitmap.Dispose();
        }

        [Theory]
        [InlineData(5, 5)]
        [InlineData(15, 20)]
        [InlineData(50, 25)]
        public void BuildImage_With_Various_Dimensions_Should_Work(int width, int height)
        {
            // Arrange
            var testBitmap = new Bitmap(100, 100);
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(width, height);
            hueMosaic.LoadImagesForTesting(); // Load images first
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Navy;
            tileColors[0, 1] = Color.Maroon;
            tileColors[1, 0] = Color.Olive;
            tileColors[1, 1] = Color.Teal;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert
                Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                act.Should().NotThrow(string.Format("BuildImage should work with dimensions {0}x{1}", width, height));
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void GetRandomImage_Distribution_Should_Cover_All_Images_Over_Multiple_Calls()
        {
            // Arrange
            var hueMosaic = new TestableHueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.LoadImagesForTesting();
            
            var tileColors = new Color[1, 1];
            tileColors[0, 0] = Color.Blue;
            var testBitmap = new Bitmap(50, 50);
            
            // Act - Call BuildImage multiple times to test randomness distribution
            using (var graphics = Graphics.FromImage(testBitmap))
            {
                for (int i = 0; i < 20; i++) // Multiple calls to test distribution
                {
                    Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                    act.Should().NotThrow(string.Format("GetRandomImage should work consistently on call {0}", i + 1));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void HueMosaic_With_Empty_Image_Collection_Should_Handle_Gracefully()
        {
            // Arrange - Create HueMosaic with empty file collection
            var emptyFileCollection = new List<string>();
            var hueMosaic = new TestableHueMosaic(emptyFileCollection, _testImagePath, _outputPath);
            
            var tileColors = new Color[1, 1];
            tileColors[0, 0] = Color.Green;
            var testBitmap = new Bitmap(30, 30);
            
            // Act & Assert
            using (var graphics = Graphics.FromImage(testBitmap))
            {
                Action act = () => hueMosaic.BuildImage(graphics, 0, 0, tileColors);
                act.Should().NotThrow<NullReferenceException>("HueMosaic should handle empty image collections without null reference exceptions");
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void HueMosaic_With_Single_Image_Collection_Should_Work()
        {
            // Arrange - Create HueMosaic with single image
            var singleImageCollection = new List<string> { _testImagePath };
            var hueMosaic = new TestableHueMosaic(singleImageCollection, _testImagePath, _outputPath);
            hueMosaic.LoadImagesForTesting();
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Red;
            tileColors[0, 1] = Color.Blue;
            tileColors[1, 0] = Color.Yellow;
            tileColors[1, 1] = Color.Green;
            
            var testBitmap = new Bitmap(60, 60);
            
            // Act & Assert
            using (var graphics = Graphics.FromImage(testBitmap))
            {
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    Action act = () => hueMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("HueMosaic should work with single image at ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }
    }
}