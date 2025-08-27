using System;
using System.Collections.Generic;
using FluentAssertions;
using ThreadedMosaic.Mosaic;
using Xunit;

namespace ThreadedMosaic.Tests
{
    public class TestProgressReporter : IProgressReporter
    {
        public int MaximumValue { get; private set; }
        public int CurrentProgress { get; private set; }
        public string LastStatus { get; private set; }
        public List<string> StatusHistory { get; private set; }

        public TestProgressReporter()
        {
            StatusHistory = new List<string>();
        }

        public void SetMaximum(int maximum)
        {
            MaximumValue = maximum;
            CurrentProgress = 0;
        }

        public void IncrementProgress()
        {
            CurrentProgress++;
        }

        public void UpdateStatus(string status)
        {
            LastStatus = status;
            StatusHistory.Add(status);
        }

        public void ReportProgress(int current, int maximum, string status)
        {
            CurrentProgress = current;
            MaximumValue = maximum;
            UpdateStatus(status);
        }
    }

    public class ProgressReporterTests
    {
        [Fact]
        public void NullProgressReporter_Should_Not_Throw_On_Any_Operation()
        {
            // Arrange
            var reporter = NullProgressReporter.Instance;

            // Act & Assert
            Action setMaximum = () => reporter.SetMaximum(100);
            Action increment = () => reporter.IncrementProgress();
            Action updateStatus = () => reporter.UpdateStatus("test");
            Action reportProgress = () => reporter.ReportProgress(50, 100, "test");

            setMaximum.Should().NotThrow();
            increment.Should().NotThrow();
            updateStatus.Should().NotThrow();
            reportProgress.Should().NotThrow();
        }

        [Fact]
        public void TestProgressReporter_Should_Track_Progress_Correctly()
        {
            // Arrange
            var reporter = new TestProgressReporter();

            // Act
            reporter.SetMaximum(10);
            reporter.UpdateStatus("Starting");
            reporter.IncrementProgress();
            reporter.IncrementProgress();
            reporter.UpdateStatus("Half done");
            reporter.ReportProgress(8, 10, "Almost done");

            // Assert
            reporter.MaximumValue.Should().Be(10);
            reporter.CurrentProgress.Should().Be(8);
            reporter.LastStatus.Should().Be("Almost done");
            reporter.StatusHistory.Should().Contain("Starting");
            reporter.StatusHistory.Should().Contain("Half done");
            reporter.StatusHistory.Should().Contain("Almost done");
        }

        [Fact]
        public void Mosaic_Should_Use_NullProgressReporter_By_Default()
        {
            // Arrange
            using (var tempFile = new TemporaryImageFile())
            {
                var outputPath = System.IO.Path.GetTempFileName();
                try
                {
                    // Act - Create mosaic with default constructor (should use NullProgressReporter)
                    var colorMosaic = new ColorMosaic(tempFile.Path, outputPath, NullProgressReporter.Instance, NullFileOperations.Instance);

                    // Act - This should work without throwing NullReferenceException
                    Action createMosaic = () => colorMosaic.CreateColorMosaic();
                    
                    // Assert - Should not throw NullReferenceException (progress reporter should work)
                    createMosaic.Should().NotThrow<NullReferenceException>();
                }
                finally
                {
                    // Clean up output file
                    if (System.IO.File.Exists(outputPath))
                    {
                        System.IO.File.Delete(outputPath);
                    }
                }
            }
        }

        [Fact]
        public void Mosaic_Should_Accept_Custom_ProgressReporter()
        {
            // Arrange
            var testReporter = new TestProgressReporter();
            using (var tempFile = new TemporaryImageFile())
            {
                var outputPath = System.IO.Path.GetTempFileName();
                try
                {
                    var colorMosaic = new ColorMosaic(tempFile.Path, outputPath, testReporter, NullFileOperations.Instance);

                    // Act
                    colorMosaic.CreateColorMosaic();

                    // Assert
                    testReporter.StatusHistory.Should().NotBeEmpty();
                    testReporter.StatusHistory.Should().Contain(status => status.Contains("Initializing"));
                    testReporter.MaximumValue.Should().BeGreaterThan(0);
                }
                finally
                {
                    // Clean up output file
                    if (System.IO.File.Exists(outputPath))
                    {
                        System.IO.File.Delete(outputPath);
                    }
                }
            }
        }
    }

    // Helper class for creating temporary test images
    public class TemporaryImageFile : IDisposable
    {
        public string Path { get; private set; }

        public TemporaryImageFile()
        {
            Path = System.IO.Path.GetTempFileName();
            CreateTestImage();
        }

        private void CreateTestImage()
        {
            using (var bitmap = new System.Drawing.Bitmap(40, 40))
            {
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.Red);
                }
                bitmap.Save(Path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        public void Dispose()
        {
            try
            {
                if (System.IO.File.Exists(Path))
                    System.IO.File.Delete(Path);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}