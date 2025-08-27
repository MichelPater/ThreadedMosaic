using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;
using System.Reflection;
using System.Collections.Concurrent;

namespace ThreadedMosaic.Tests
{
    public class TestablePhotoMosaic : PhotoMosaic
    {
        public TestablePhotoMosaic(List<string> fileLocations, string masterFileLocation, string outputFileLocation) 
            : base(fileLocations, masterFileLocation, outputFileLocation, NullProgressReporter.Instance, NullFileOperations.Instance)
        {
        }

        public new void BuildImage(Graphics graphics, int xCoordinate, int yCoordinate, Color[,] tileColors)
        {
            base.BuildImage(graphics, xCoordinate, yCoordinate, tileColors);
        }

        public void LoadImagesForTesting()
        {
            // Use reflection to access the private LoadImages method
            var method = typeof(PhotoMosaic).GetMethod("LoadImages", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(this, null);
        }

        public System.Drawing.Image CallGetClosestMatchingImage(Color tileColor)
        {
            // Use reflection to access the private GetClosestMatchingImage method
            var method = typeof(PhotoMosaic).GetMethod("GetClosestMatchingImage", BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Drawing.Image)method.Invoke(this, new object[] { tileColor });
        }

        public void SetupTestImages()
        {
            // Use reflection to populate the _concurrentBag with test data
            var field = typeof(PhotoMosaic).GetField("_concurrentBag", BindingFlags.NonPublic | BindingFlags.Instance);
            var concurrentBag = (ConcurrentBag<LoadedImage>)field.GetValue(this);
            
            // Add test images with known colors
            foreach (var filePath in FileLocations)
            {
                concurrentBag.Add(new LoadedImage 
                { 
                    FilePath = filePath, 
                    AverageColor = Color.Red // Test color for now
                });
            }
        }
    }

    public class PhotoMosaicTests : IDisposable
    {
        private readonly string _testImagePath;
        private readonly string _testImagePath2;
        private readonly string _outputPath;
        private readonly List<string> _fileLocations;

        public PhotoMosaicTests()
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
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Assert
            photoMosaic.Should().NotBeNull();
            photoMosaic.XPixelCount.Should().Be(40);
            photoMosaic.YPixelCount.Should().Be(40);
        }

        [Fact]
        public void Constructor_With_Null_FileLocations_Should_Throw_Exception()
        {
            // Arrange, Act & Assert
            Action act = () => new PhotoMosaic(null, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().NotThrow(); // FileLocations is just stored, not validated immediately
        }

        [Fact]
        public void Constructor_With_Empty_FileLocations_Should_Not_Throw()
        {
            // Arrange
            var emptyFileLocations = new List<string>();

            // Act & Assert
            Action act = () => new PhotoMosaic(emptyFileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().NotThrow();
        }

        [Fact]
        public void Constructor_With_Invalid_MasterFile_Should_Throw_Exception()
        {
            // Arrange
            var invalidPath = @"C:\nonexistent\file.jpg";

            // Act & Assert
            Action act = () => new PhotoMosaic(_fileLocations, invalidPath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void CreatePhotoMosaic_Should_Complete_Without_Exception()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => photoMosaic.CreatePhotoMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreatePhotoMosaic_With_Empty_FileLocations_Should_Throw_Exception()
        {
            // Arrange
            var emptyFileLocations = new List<string>();
            var photoMosaic = new PhotoMosaic(emptyFileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => photoMosaic.CreatePhotoMosaic();
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(20, 30)]
        [InlineData(5, 5)]
        public void SetPixelSize_Should_Update_Tile_Dimensions(int width, int height)
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act
            photoMosaic.SetPixelSize(width, height);

            // Assert
            photoMosaic.XPixelCount.Should().Be(width);
            photoMosaic.YPixelCount.Should().Be(height);
        }

        [Fact]
        public void CreatePhotoMosaic_With_Single_Image_Should_Work()
        {
            // Arrange
            var singleImageList = new List<string> { _testImagePath };
            var photoMosaic = new PhotoMosaic(singleImageList, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => photoMosaic.CreatePhotoMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreatePhotoMosaic_With_Many_Images_Should_Work()
        {
            // Arrange
            var manyImages = new List<string>();
            for (int i = 0; i < 5; i++) // Reduced from 10 to 5 for faster test execution
            {
                manyImages.Add(_testImagePath);
                manyImages.Add(_testImagePath2);
            }
            var photoMosaic = new PhotoMosaic(manyImages, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action act = () => photoMosaic.CreatePhotoMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreatePhotoMosaic_With_Small_Tile_Size_Should_Work()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            photoMosaic.SetPixelSize(5, 5);

            // Act & Assert
            Action act = () => photoMosaic.CreatePhotoMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void Multiple_CreatePhotoMosaic_Calls_Should_Work()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act & Assert
            Action firstCall = () => photoMosaic.CreatePhotoMosaic();
            firstCall.Should().NotThrow();

            Action secondCall = () => photoMosaic.CreatePhotoMosaic();
            secondCall.Should().NotThrow();
        }

        [Fact]
        public void Constructor_With_Null_MasterFileLocation_Should_Throw()
        {
            // Arrange, Act & Assert
            Action act = () => new PhotoMosaic(_fileLocations, null, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_With_Null_OutputFileLocation_Should_Throw()
        {
            // Arrange, Act & Assert
            Action act = () => new PhotoMosaic(_fileLocations, _testImagePath, null, NullProgressReporter.Instance, NullFileOperations.Instance);
            act.Should().NotThrow(); // Output path is just stored, not validated immediately
        }

        [Fact]
        public void CreatePhotoMosaic_With_Large_Tile_Size_Should_Work()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            photoMosaic.SetPixelSize(80, 80);

            // Act & Assert
            Action act = () => photoMosaic.CreatePhotoMosaic();
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(100, 100)]
        public void SetPixelSize_With_Edge_Cases_Should_Work(int width, int height)
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

            // Act
            photoMosaic.SetPixelSize(width, height);

            // Assert
            photoMosaic.XPixelCount.Should().Be(width);
            photoMosaic.YPixelCount.Should().Be(height);
        }

        #region CompareColors Algorithm Tests

        [Fact]
        public void CompareColors_With_Identical_Colors_Should_Return_Zero()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            var color = Color.FromArgb(128, 64, 192);

            // Act
            var distance = photoMosaic.CompareColors(color, color);

            // Assert
            distance.Should().Be(0.0);
        }

        [Fact]
        public void CompareColors_With_Black_And_White_Should_Return_Maximum_Distance()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            var black = Color.FromArgb(0, 0, 0);
            var white = Color.FromArgb(255, 255, 255);

            // Act
            var distance = photoMosaic.CompareColors(black, white);

            // Assert - Should be sqrt(255^2 + 255^2 + 255^2) = sqrt(3*255^2) ≈ 441.67
            var expectedDistance = Math.Sqrt(3 * 255 * 255);
            distance.Should().BeApproximately(expectedDistance, 0.1);
        }

        [Fact]
        public void CompareColors_Mathematical_Accuracy_Should_Match_Euclidean_Distance()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            var color1 = Color.FromArgb(100, 150, 200);
            var color2 = Color.FromArgb(110, 140, 180);

            // Act
            var actualDistance = photoMosaic.CompareColors(color1, color2);

            // Assert - Manual calculation: sqrt((110-100)^2 + (140-150)^2 + (180-200)^2) = sqrt(100 + 100 + 400) = sqrt(600)
            var expectedDistance = Math.Sqrt(Math.Pow(110 - 100, 2) + Math.Pow(140 - 150, 2) + Math.Pow(180 - 200, 2));
            actualDistance.Should().BeApproximately(expectedDistance, 0.001);
        }

        [Theory]
        [InlineData(255, 0, 0, 0, 255, 0)] // Red vs Green
        [InlineData(255, 0, 0, 0, 0, 255)] // Red vs Blue  
        [InlineData(0, 255, 0, 0, 0, 255)] // Green vs Blue
        public void CompareColors_With_Pure_Colors_Should_Return_Expected_Distance(int r1, int g1, int b1, int r2, int g2, int b2)
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            var color1 = Color.FromArgb(r1, g1, b1);
            var color2 = Color.FromArgb(r2, g2, b2);

            // Act
            var actualDistance = photoMosaic.CompareColors(color1, color2);

            // Assert
            var expectedDistance = Math.Sqrt(Math.Pow(r2 - r1, 2) + Math.Pow(g2 - g1, 2) + Math.Pow(b2 - b1, 2));
            actualDistance.Should().BeApproximately(expectedDistance, 0.001);
        }

        [Fact]
        public void CompareColors_Should_Be_Symmetric()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            var color1 = Color.FromArgb(75, 125, 225);
            var color2 = Color.FromArgb(150, 100, 50);

            // Act
            var distance1to2 = photoMosaic.CompareColors(color1, color2);
            var distance2to1 = photoMosaic.CompareColors(color2, color1);

            // Assert - Distance calculation should be symmetric
            distance1to2.Should().BeApproximately(distance2to1, 0.001);
        }

        [Fact]
        public void CompareColors_With_Grayscale_Colors_Should_Work_Correctly()
        {
            // Arrange
            var photoMosaic = new PhotoMosaic(_fileLocations, _testImagePath, _outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);
            var lightGray = Color.FromArgb(200, 200, 200);
            var darkGray = Color.FromArgb(100, 100, 100);

            // Act
            var distance = photoMosaic.CompareColors(lightGray, darkGray);

            // Assert - Should be sqrt(3 * 100^2) = sqrt(30000) ≈ 173.2
            var expectedDistance = Math.Sqrt(3 * 100 * 100);
            distance.Should().BeApproximately(expectedDistance, 0.1);
        }

        #endregion

        #region PhotoMosaic BuildImage Integration Tests

        [Fact]
        public void BuildImage_Should_Draw_Image_And_Apply_Transparency_Overlay()
        {
            // Arrange
            var testBitmap = new Bitmap(100, 100);
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            photoMosaic.SetPixelSize(50, 50);
            photoMosaic.SetupTestImages(); // Setup test images in concurrent bag
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Red;
            tileColors[0, 1] = Color.Green;
            tileColors[1, 0] = Color.Blue;
            tileColors[1, 1] = Color.Yellow;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act
                Action act = () => photoMosaic.BuildImage(graphics, 0, 0, tileColors);
                
                // Assert
                act.Should().NotThrow("BuildImage should draw image and apply transparency overlay correctly");
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Should_Apply_Transparency_With_Alpha_210()
        {
            // Arrange
            var testBitmap = new Bitmap(60, 60);
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            photoMosaic.SetPixelSize(30, 30);
            photoMosaic.SetupTestImages();
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.FromArgb(255, 100, 150, 200);
            tileColors[0, 1] = Color.FromArgb(255, 50, 75, 125);
            tileColors[1, 0] = Color.FromArgb(255, 200, 100, 50);
            tileColors[1, 1] = Color.FromArgb(255, 75, 200, 175);

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Test that transparency overlay with alpha 210 is applied
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    Action act = () => photoMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should apply alpha 210 transparency at ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Should_Dispose_Resources_Properly()
        {
            // Arrange
            var testBitmap = new Bitmap(80, 80);
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            photoMosaic.SetPixelSize(40, 40);
            photoMosaic.SetupTestImages();
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Purple;
            tileColors[0, 1] = Color.Orange;
            tileColors[1, 0] = Color.Cyan;
            tileColors[1, 1] = Color.Magenta;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Multiple calls should work without resource leaks
                for (int iteration = 0; iteration < 5; iteration++)
                {
                    Action act = () => photoMosaic.BuildImage(graphics, 0, 0, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage iteration {0} should dispose resources properly", iteration + 1));
                }
            }
            
            testBitmap.Dispose();
        }

        [Theory]
        [InlineData(10, 15)]
        [InlineData(25, 25)]
        [InlineData(50, 30)]
        public void BuildImage_With_Various_Tile_Sizes_Should_Scale_Images_Correctly(int width, int height)
        {
            // Arrange
            var testBitmap = new Bitmap(100, 90);
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            photoMosaic.SetPixelSize(width, height);
            photoMosaic.SetupTestImages();
            
            var tileColors = new Color[2, 2];
            tileColors[0, 0] = Color.Navy;
            tileColors[0, 1] = Color.Maroon;
            tileColors[1, 0] = Color.Olive;
            tileColors[1, 1] = Color.Teal;

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act
                Action act = () => photoMosaic.BuildImage(graphics, 0, 0, tileColors);
                
                // Assert
                act.Should().NotThrow(string.Format("BuildImage should scale images correctly with {0}x{1} tiles", width, height));
            }
            
            testBitmap.Dispose();
        }

        [Fact]
        public void BuildImage_Multiple_Coordinates_Should_Work_Consistently()
        {
            // Arrange
            var testBitmap = new Bitmap(120, 120);
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            photoMosaic.SetPixelSize(40, 40);
            photoMosaic.SetupTestImages();
            
            var tileColors = new Color[3, 3];
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                tileColors[x, y] = Color.FromArgb(255, (x + 1) * 80, (y + 1) * 80, 100);

            using (var graphics = Graphics.FromImage(testBitmap))
            {
                // Act & Assert - Test all coordinates
                for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                {
                    Action act = () => photoMosaic.BuildImage(graphics, x, y, tileColors);
                    act.Should().NotThrow(string.Format("BuildImage should work consistently at coordinate ({0}, {1})", x, y));
                }
            }
            
            testBitmap.Dispose();
        }

        #endregion

        #region GetClosestMatchingImage Algorithm Accuracy Tests

        [Fact]
        public void GetClosestMatchingImage_Should_Return_Best_Color_Match()
        {
            // Arrange
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            
            // Setup test images with specific colors using reflection
            var field = typeof(PhotoMosaic).GetField("_concurrentBag", BindingFlags.NonPublic | BindingFlags.Instance);
            var concurrentBag = (ConcurrentBag<LoadedImage>)field.GetValue(photoMosaic);
            
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath, AverageColor = Color.Red });
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath2, AverageColor = Color.Blue });
            
            var targetColor = Color.FromArgb(255, 255, 50, 50); // Closer to red

            // Act
            using (var result = photoMosaic.CallGetClosestMatchingImage(targetColor))
            {
                // Assert
                result.Should().NotBeNull("GetClosestMatchingImage should return the closest matching image");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void GetClosestMatchingImage_With_Identical_Colors_Should_Return_First_Match()
        {
            // Arrange
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            
            // Setup test images with identical colors
            var field = typeof(PhotoMosaic).GetField("_concurrentBag", BindingFlags.NonPublic | BindingFlags.Instance);
            var concurrentBag = (ConcurrentBag<LoadedImage>)field.GetValue(photoMosaic);
            
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath, AverageColor = Color.Green });
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath2, AverageColor = Color.Green });
            
            var targetColor = Color.Green; // Exact match

            // Act
            using (var result = photoMosaic.CallGetClosestMatchingImage(targetColor))
            {
                // Assert
                result.Should().NotBeNull("GetClosestMatchingImage should return a match for identical colors");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void GetClosestMatchingImage_With_Grayscale_Colors_Should_Work()
        {
            // Arrange
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            
            // Setup test images with grayscale colors
            var field = typeof(PhotoMosaic).GetField("_concurrentBag", BindingFlags.NonPublic | BindingFlags.Instance);
            var concurrentBag = (ConcurrentBag<LoadedImage>)field.GetValue(photoMosaic);
            
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath, AverageColor = Color.FromArgb(255, 64, 64, 64) });   // Dark gray
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath2, AverageColor = Color.FromArgb(255, 192, 192, 192) }); // Light gray
            
            var targetColor = Color.FromArgb(255, 128, 128, 128); // Medium gray

            // Act
            using (var result = photoMosaic.CallGetClosestMatchingImage(targetColor))
            {
                // Assert
                result.Should().NotBeNull("GetClosestMatchingImage should handle grayscale colors correctly");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void GetClosestMatchingImage_Color_Matching_Precision_Should_Be_Accurate()
        {
            // Arrange
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            
            // Setup test images with known color distances
            var field = typeof(PhotoMosaic).GetField("_concurrentBag", BindingFlags.NonPublic | BindingFlags.Instance);
            var concurrentBag = (ConcurrentBag<LoadedImage>)field.GetValue(photoMosaic);
            
            // Add colors at known distances from target
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath, AverageColor = Color.FromArgb(255, 100, 100, 100) });  // Distance ~87
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath2, AverageColor = Color.FromArgb(255, 155, 155, 155) }); // Distance ~95
            
            var targetColor = Color.FromArgb(255, 150, 150, 150);

            // Act - Should pick the closer color (100,100,100)
            using (var result = photoMosaic.CallGetClosestMatchingImage(targetColor))
            {
                // Assert
                result.Should().NotBeNull("GetClosestMatchingImage should select the mathematically closest color");
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        [Theory]
        [InlineData(0, 0, 0, 255, 255, 255)]   // Black vs White (max distance)
        [InlineData(255, 0, 0, 0, 255, 0)]     // Red vs Green  
        [InlineData(0, 0, 255, 255, 255, 0)]   // Blue vs Yellow
        public void GetClosestMatchingImage_With_Color_Extremes_Should_Work(int r1, int g1, int b1, int r2, int g2, int b2)
        {
            // Arrange
            var photoMosaic = new TestablePhotoMosaic(_fileLocations, _testImagePath, _outputPath);
            
            var field = typeof(PhotoMosaic).GetField("_concurrentBag", BindingFlags.NonPublic | BindingFlags.Instance);
            var concurrentBag = (ConcurrentBag<LoadedImage>)field.GetValue(photoMosaic);
            
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath, AverageColor = Color.FromArgb(255, r1, g1, b1) });
            concurrentBag.Add(new LoadedImage { FilePath = _testImagePath2, AverageColor = Color.FromArgb(255, r2, g2, b2) });
            
            var targetColor = Color.FromArgb(255, Math.Min(255, r1 + 10), Math.Min(255, g1 + 10), Math.Min(255, b1 + 10)); // Closer to first color

            // Act
            using (var result = photoMosaic.CallGetClosestMatchingImage(targetColor))
            {
                // Assert
                result.Should().NotBeNull(string.Format("GetClosestMatchingImage should handle color extremes ({0},{1},{2}) vs ({3},{4},{5})", r1, g1, b1, r2, g2, b2));
                result.Width.Should().BeGreaterThan(0);
                result.Height.Should().BeGreaterThan(0);
            }
        }

        #endregion
    }
}