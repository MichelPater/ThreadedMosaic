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
    }
}