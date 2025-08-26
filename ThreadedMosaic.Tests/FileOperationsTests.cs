using System;
using System.IO;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class FileOperationsTests : IDisposable
    {
        private readonly string _testImagePath;
        private readonly string _outputPath;

        public FileOperationsTests()
        {
            _testImagePath = Path.GetTempFileName();
            _outputPath = Path.GetTempFileName();
            CreateTestImage(_testImagePath);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testImagePath))
                    File.Delete(_testImagePath);
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
        public void NullFileOperations_Should_Not_Open_Files()
        {
            // Arrange
            var fileOps = NullFileOperations.Instance;

            // Act & Assert - Should not throw or open any files
            Action act = () => fileOps.OpenImageFile(_testImagePath);
            act.Should().NotThrow();
        }

        [Fact]
        public void DefaultFileOperations_Should_Be_Available()
        {
            // Arrange & Act
            var fileOps = DefaultFileOperations.Instance;

            // Assert
            fileOps.Should().NotBeNull();
        }

        [Fact]
        public void ColorMosaic_With_NullFileOperations_Should_Not_Open_Files()
        {
            // Arrange
            var testFileOps = new TestFileOperations();
            var colorMosaic = new ColorMosaic(_testImagePath, _outputPath, NullProgressReporter.Instance, testFileOps);

            // Act
            colorMosaic.CreateColorMosaic();

            // Assert - Verify that OpenImageFile was called but didn't actually open anything
            testFileOps.WasCalled.Should().BeTrue();
            testFileOps.CalledWith.Should().Be(_outputPath);
        }

        private class TestFileOperations : IFileOperations
        {
            public bool WasCalled { get; private set; }
            public string CalledWith { get; private set; }

            public void OpenImageFile(string imageLocation)
            {
                WasCalled = true;
                CalledWith = imageLocation;
                // Don't actually open the file - just track that the method was called
            }
        }
    }
}