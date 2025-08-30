using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.Services;
using Xunit;

namespace ThreadedMosaic.Tests.Services
{
    /// <summary>
    /// Tests for TempFileCleanupService to verify temporary file management functionality
    /// </summary>
    public class TempFileCleanupServiceTests : IDisposable
    {
        private readonly Mock<ILogger<TempFileCleanupService>> _mockLogger;
        private readonly TempFileCleanupService _cleanupService;
        private readonly string _testDirectory;

        public TempFileCleanupServiceTests()
        {
            _mockLogger = new Mock<ILogger<TempFileCleanupService>>();
            _testDirectory = Path.Combine(Path.GetTempPath(), "TempFileCleanupServiceTests", Guid.NewGuid().ToString());
            
            // Create a real configuration instead of mocking it
            var configurationData = new Dictionary<string, string>
            {
                ["MosaicConfiguration:FileManagement:CleanupIntervalHours"] = "0.001",
                ["MosaicConfiguration:FileManagement:TempFileCleanupAgeDays"] = "1", // Use 1 day instead of 0
                ["MosaicConfiguration:FileManagement:MinimumFreeDiskSpaceMB"] = "1024",
                ["MosaicConfiguration:FileManagement:TempDirectory"] = _testDirectory
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData!)
                .Build();

            Directory.CreateDirectory(_testDirectory);
            _cleanupService = new TempFileCleanupService(_mockLogger.Object, configuration);
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Assert - service was created in constructor
            _cleanupService.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();

            // Act & Assert
            FluentActions.Invoking(() => new TempFileCleanupService(null!, configuration))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_NullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions.Invoking(() => new TempFileCleanupService(_mockLogger.Object, null!))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("configuration");
        }

        [Fact]
        public async Task ForceCleanupAsync_WithExistingFiles_DeletesOldFiles()
        {
            // Arrange - Create old test files
            var oldFile1 = Path.Combine(_testDirectory, "old1.txt");
            var oldFile2 = Path.Combine(_testDirectory, "old2.txt");
            
            await File.WriteAllTextAsync(oldFile1, "old content 1");
            await File.WriteAllTextAsync(oldFile2, "old content 2");
            
            // Make files appear old by setting their timestamps
            var oldTime = DateTime.Now.AddDays(-2);
            File.SetLastWriteTime(oldFile1, oldTime);
            File.SetLastWriteTime(oldFile2, oldTime);

            // Act
            var result = await _cleanupService.ForceCleanupAsync();

            // Assert
            result.Should().NotBeNull();
            result.DeletedFiles.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ForceCleanupAsync_WithEmptyDirectory_ReturnsZeroResults()
        {
            // Act
            var result = await _cleanupService.ForceCleanupAsync();

            // Assert
            result.Should().NotBeNull();
            result.DeletedFiles.Should().Be(0);
            result.DeletedDirectories.Should().Be(0);
            result.ReclaimedBytes.Should().Be(0);
        }

        [Fact]
        public async Task ForceCleanupAsync_WithNewerFiles_DoesNotDeleteThem()
        {
            // Arrange - Create new files with explicit recent timestamp
            var newFile = Path.Combine(_testDirectory, "new.txt");
            await File.WriteAllTextAsync(newFile, "new content");
            
            // Ensure file timestamp is definitely newer than the cleanup threshold (less than 1 day old)
            var recentTime = DateTime.Now.AddHours(-1); // 1 hour ago
            File.SetLastWriteTime(newFile, recentTime);

            // Act
            var result = await _cleanupService.ForceCleanupAsync();

            // Assert
            File.Exists(newFile).Should().BeTrue(); // File should still exist
            result.DeletedFiles.Should().Be(0);
        }

        [Fact]
        public async Task GetAvailableDiskSpaceAsync_ValidCall_ReturnsPositiveValue()
        {
            // Act
            var diskSpace = await _cleanupService.GetAvailableDiskSpaceAsync();

            // Assert
            diskSpace.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ForceCleanupAsync_WithCancellationToken_AcceptsToken()
        {
            // Arrange - Create a few old files
            for (int i = 0; i < 3; i++)
            {
                var oldFile = Path.Combine(_testDirectory, $"old_{i}.txt");
                await File.WriteAllTextAsync(oldFile, "old content");
                File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-2)); // Make them old
            }

            var cancellationTokenSource = new CancellationTokenSource();
            // Don't cancel immediately - test that the method accepts the token parameter

            // Act & Assert - Should not throw and should accept the cancellation token
            var result = await _cleanupService.ForceCleanupAsync(cancellationTokenSource.Token);

            // Assert - Method completes successfully with cancellation token
            result.Should().NotBeNull();
            result.DeletedFiles.Should().BeGreaterThan(0); // Files should be deleted
        }

        [Fact]
        public void CleanupResult_Properties_WorkCorrectly()
        {
            // Arrange
            var reclaimedBytes = 2 * 1024 * 1024; // 2MB
            var result = new CleanupResult(5, 2, reclaimedBytes);

            // Act & Assert
            result.DeletedFiles.Should().Be(5);
            result.DeletedDirectories.Should().Be(2);
            result.ReclaimedBytes.Should().Be(reclaimedBytes);
            result.ReclaimedMB.Should().Be(2.0);
        }

        [Fact]
        public async Task ForceCleanupAsync_WithDirectoryStructure_CleansDirectoriesCorrectly()
        {
            // Arrange - Create nested directory structure with old files
            var subDir = Path.Combine(_testDirectory, "subdir");
            Directory.CreateDirectory(subDir);
            
            var oldFile = Path.Combine(subDir, "old.txt");
            await File.WriteAllTextAsync(oldFile, "old content");
            
            // Make file appear old
            var oldTime = DateTime.Now.AddDays(-2);
            File.SetLastWriteTime(oldFile, oldTime);
            Directory.SetLastWriteTime(subDir, oldTime);

            // Act
            var result = await _cleanupService.ForceCleanupAsync();

            // Assert
            result.Should().NotBeNull();
            File.Exists(oldFile).Should().BeFalse(); // File should be deleted
        }

        [Fact]
        public async Task ForceCleanupAsync_WithLargeFiles_CalculatesReclaimedBytesCorrectly()
        {
            // Arrange
            var largeFile = Path.Combine(_testDirectory, "large.txt");
            var content = new string('A', 1024); // 1KB of content
            await File.WriteAllTextAsync(largeFile, content);
            
            // Make file appear old
            File.SetLastWriteTime(largeFile, DateTime.Now.AddDays(-2));

            // Act
            var result = await _cleanupService.ForceCleanupAsync();

            // Assert
            result.Should().NotBeNull();
            result.DeletedFiles.Should().Be(1);
            result.ReclaimedBytes.Should().BeGreaterThan(1000); // Should be at least 1KB
            result.ReclaimedMB.Should().BeGreaterThan(0);
        }

        public void Dispose()
        {
            _cleanupService?.Dispose();
            
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }
}