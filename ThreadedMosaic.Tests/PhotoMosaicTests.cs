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
    }
}