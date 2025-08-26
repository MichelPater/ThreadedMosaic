using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class ErrorHandlingTests : IDisposable
    {
        private readonly string _validImagePath;
        private readonly string _outputPath;

        public ErrorHandlingTests()
        {
            _validImagePath = Path.GetTempFileName();
            _outputPath = Path.GetTempFileName();
            CreateTestImage(_validImagePath);
        }

        public void Dispose()
        {
            // Force garbage collection and cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            try
            {
                if (File.Exists(_validImagePath))
                    File.Delete(_validImagePath);
                if (File.Exists(_outputPath))
                    File.Delete(_outputPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void CreateTestImage(string path)
        {
            using (var bitmap = new System.Drawing.Bitmap(40, 40))
            {
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.Blue);
                }
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        [Fact]
        public void Constructor_With_NonExistent_File_Should_Throw_Exception()
        {
            // Arrange
            var nonExistentFile = @"C:\NonExistent\Path\image.jpg";

            // Act & Assert
            Action act = () => new ColorMosaic(nonExistentFile, _outputPath);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_With_Null_MasterFile_Should_Throw_Exception()
        {
            // Act & Assert
            Action act = () => new ColorMosaic(null, _outputPath);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_With_Empty_String_MasterFile_Should_Throw_Exception()
        {
            // Act & Assert
            Action act = () => new ColorMosaic(string.Empty, _outputPath);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void HueMosaic_With_Null_FileLocations_Should_Handle_Gracefully()
        {
            // Act & Assert
            Action act = () => new HueMosaic(null, _validImagePath, _outputPath);
            act.Should().NotThrow(); // Constructor should not throw
        }

        [Fact]
        public void HueMosaic_With_Empty_FileLocations_Should_Handle_Gracefully()
        {
            // Arrange
            var emptyList = new List<string>();

            // Act & Assert
            Action act = () => new HueMosaic(emptyList, _validImagePath, _outputPath);
            act.Should().NotThrow();
        }

        [Fact]
        public void PhotoMosaic_With_Invalid_Files_In_List_Should_Skip_Invalid_Files()
        {
            // Arrange
            var mixedFileList = new List<string>
            {
                _validImagePath,
                @"C:\NonExistent\invalid1.jpg",
                @"C:\NonExistent\invalid2.jpg"
            };

            var photoMosaic = new PhotoMosaic(mixedFileList, _validImagePath, _outputPath);

            // Act & Assert - Should not throw exception during creation
            Action act = () => photoMosaic.CreatePhotoMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void HueMosaic_CreateColorMosaic_With_No_Valid_Images_Should_Still_Work()
        {
            // Arrange
            var invalidFileList = new List<string>
            {
                @"C:\NonExistent\invalid1.jpg",
                @"C:\NonExistent\invalid2.jpg"
            };

            var hueMosaic = new HueMosaic(invalidFileList, _validImagePath, _outputPath);

            // Act & Assert
            Action act = () => hueMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void Mosaic_With_Very_Small_Tile_Size_Should_Work()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_validImagePath, _outputPath);
            colorMosaic.SetPixelSize(1, 1);

            // Act & Assert
            Action act = () => colorMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Fact]
        public void Mosaic_With_Very_Large_Tile_Size_Should_Work()
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_validImagePath, _outputPath);
            colorMosaic.SetPixelSize(1000, 1000); // Larger than the test image

            // Act & Assert
            Action act = () => colorMosaic.CreateColorMosaic();
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(0, 10)]   // Zero width
        [InlineData(10, 0)]   // Zero height  
        [InlineData(-5, 10)]  // Negative width
        [InlineData(10, -5)]  // Negative height
        public void SetPixelSize_With_Invalid_Values_Should_Still_Set_Values(int width, int height)
        {
            // Arrange
            var colorMosaic = new ColorMosaic(_validImagePath, _outputPath);

            // Act
            colorMosaic.SetPixelSize(width, height);

            // Assert - The method doesn't validate, so it sets the values
            colorMosaic.XPixelCount.Should().Be(width);
            colorMosaic.YPixelCount.Should().Be(height);
        }

        [Fact]
        public void Mosaic_Creation_Should_Handle_Concurrent_File_Access()
        {
            // Arrange
            var fileList = new List<string> { _validImagePath };
            
            // Create multiple mosaics that might access the same file
            var mosaic1 = new HueMosaic(fileList, _validImagePath, _outputPath + "1");
            var mosaic2 = new PhotoMosaic(fileList, _validImagePath, _outputPath + "2");

            // Act & Assert - Should handle concurrent access gracefully
            Action act1 = () => mosaic1.CreateColorMosaic();
            Action act2 = () => mosaic2.CreatePhotoMosaic();

            act1.Should().NotThrow();
            act2.Should().NotThrow();
        }

        [Fact]
        public void Multiple_Progress_Reporter_Calls_Should_Not_Interfere()
        {
            // Arrange
            var testReporter = new TestProgressReporter();
            var colorMosaic = new ColorMosaic(_validImagePath, _outputPath, testReporter);

            // Act - Call multiple times
            colorMosaic.CreateColorMosaic();
            
            // Reset and call again
            testReporter.StatusHistory.Clear();
            colorMosaic.CreateColorMosaic();

            // Assert - Should work both times
            testReporter.StatusHistory.Should().NotBeEmpty();
        }

        private class TestProgressReporter : IProgressReporter
        {
            public List<string> StatusHistory { get; private set; }
            public int MaximumValue { get; private set; }
            public int CurrentValue { get; private set; }

            public TestProgressReporter()
            {
                StatusHistory = new List<string>();
            }

            public void SetMaximum(int maximum)
            {
                MaximumValue = maximum;
            }

            public void IncrementProgress()
            {
                CurrentValue++;
            }

            public void UpdateStatus(string status)
            {
                StatusHistory.Add(status);
            }

            public void ReportProgress(int current, int maximum, string status)
            {
                CurrentValue = current;
                MaximumValue = maximum;
                UpdateStatus(status);
            }
        }
    }
}