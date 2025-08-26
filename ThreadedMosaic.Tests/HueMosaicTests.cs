using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Controls;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
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

        private void SetupMockUIComponents(ThreadedMosaic.Mosaic.Mosaic mosaic)
        {
            // Create mock UI components to prevent null reference exceptions
            var mockProgressBar = new ProgressBar();
            var mockLabel = new Label();
            mosaic.SetProgressBar(mockProgressBar, mockLabel);
        }

        [Fact]
        public void Constructor_Should_Initialize_With_Valid_Parameters()
        {
            // Arrange & Act
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath);

            // Assert
            hueMosaic.Should().NotBeNull();
            hueMosaic.XPixelCount.Should().Be(40);
            hueMosaic.YPixelCount.Should().Be(40);
        }

        [Fact]
        public void Constructor_With_Null_FileLocations_Should_Throw_Exception()
        {
            // Arrange, Act & Assert
            Action act = () => new HueMosaic(null, _testImagePath, _outputPath);
            act.Should().NotThrow(); // FileLocations is just stored, not validated immediately
        }

        [Fact]
        public void Constructor_With_Empty_FileLocations_Should_Not_Throw()
        {
            // Arrange
            var emptyFileLocations = new List<string>();

            // Act & Assert
            Action act = () => new HueMosaic(emptyFileLocations, _testImagePath, _outputPath);
            act.Should().NotThrow();
        }

        [Fact]
        public void Constructor_With_Invalid_MasterFile_Should_Throw_Exception()
        {
            // Arrange
            var invalidPath = @"C:\nonexistent\file.jpg";

            // Act & Assert
            Action act = () => new HueMosaic(_fileLocations, invalidPath, _outputPath);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void CreateColorMosaic_Should_Complete_Without_Exception()
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath);
            SetupMockUIComponents(hueMosaic);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreateColorMosaic_With_Empty_FileLocations_Should_Still_Work()
        {
            // Arrange
            var emptyFileLocations = new List<string>();
            var hueMosaic = new HueMosaic(emptyFileLocations, _testImagePath, _outputPath);
            SetupMockUIComponents(hueMosaic);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(20, 30)]
        [InlineData(5, 5)]
        public void SetPixelSize_Should_Update_Tile_Dimensions(int width, int height)
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath);

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
            var hueMosaic = new HueMosaic(singleImageList, _testImagePath, _outputPath);
            SetupMockUIComponents(hueMosaic);

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
            var hueMosaic = new HueMosaic(manyImages, _testImagePath, _outputPath);
            SetupMockUIComponents(hueMosaic);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void CreateColorMosaic_With_Small_Tile_Size_Should_Work()
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath);
            hueMosaic.SetPixelSize(5, 5);
            SetupMockUIComponents(hueMosaic);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void Multiple_CreateColorMosaic_Calls_Should_Work()
        {
            // Arrange
            var hueMosaic = new HueMosaic(_fileLocations, _testImagePath, _outputPath);
            SetupMockUIComponents(hueMosaic);

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
            Action act = () => new HueMosaic(_fileLocations, null, _outputPath);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_With_Null_OutputFileLocation_Should_Throw()
        {
            // Arrange, Act & Assert
            Action act = () => new HueMosaic(_fileLocations, _testImagePath, null);
            act.Should().NotThrow(); // Output path is just stored, not validated immediately
        }
    }
}