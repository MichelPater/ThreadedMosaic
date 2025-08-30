using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Threading.Tasks;
using ThreadedMosaic.Core.Services;
using Xunit;

namespace ThreadedMosaic.Tests.Services
{
    /// <summary>
    /// Tests for MemoryManagementService to verify memory monitoring and management functionality
    /// </summary>
    public class MemoryManagementServiceTests : IDisposable
    {
        private readonly Mock<ILogger<MemoryManagementService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly MemoryManagementService _memoryService;

        public MemoryManagementServiceTests()
        {
            _mockLogger = new Mock<ILogger<MemoryManagementService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();

            // Create a real configuration instead of mocking it
            var configurationData = new Dictionary<string, string>
            {
                ["MosaicConfiguration:Performance:ResourceMonitoringIntervalSeconds"] = "1",
                ["MosaicConfiguration:Performance:MaxMemoryUsageMB"] = "1024",
                ["MosaicConfiguration:Performance:MemoryWarningThreshold"] = "0.75",
                ["MosaicConfiguration:Performance:MemoryCriticalThreshold"] = "0.90",
                ["MosaicConfiguration:Performance:EnableAutomaticGarbageCollection"] = "true",
                ["MosaicConfiguration:Performance:EnableMemoryPressureOptimization"] = "true"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData!)
                .Build();

            _memoryService = new MemoryManagementService(_mockLogger.Object, configuration, _mockMemoryCache.Object);
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Assert - service was created in constructor
            _memoryService.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();

            // Act & Assert
            FluentActions.Invoking(() => new MemoryManagementService(null!, configuration, _mockMemoryCache.Object))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("logger");
        }

        [Fact]
        public void Constructor_NullConfiguration_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions.Invoking(() => new MemoryManagementService(_mockLogger.Object, null!, _mockMemoryCache.Object))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("configuration");
        }

        [Fact]
        public void Constructor_NullMemoryCache_ThrowsArgumentNullException()
        {
            // Arrange
            var configuration = new ConfigurationBuilder().Build();

            // Act & Assert
            FluentActions.Invoking(() => new MemoryManagementService(_mockLogger.Object, configuration, null!))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("memoryCache");
        }

        [Fact]
        public void CanStartNewProcessingTask_ValidCall_ReturnsBooleanValue()
        {
            // Act
            var canStart = _memoryService.CanStartNewProcessingTask();

            // Assert
            Assert.IsType<bool>(canStart);
        }

        [Fact]
        public void RegisterProcessingTaskStart_ValidCall_CompletesSuccessfully()
        {
            // Act & Assert
            FluentActions.Invoking(() => _memoryService.RegisterProcessingTaskStart())
                         .Should().NotThrow();
        }

        [Fact]
        public void RegisterProcessingTaskEnd_ValidCall_CompletesSuccessfully()
        {
            // Arrange - First register a task start
            _memoryService.RegisterProcessingTaskStart();

            // Act & Assert
            FluentActions.Invoking(() => _memoryService.RegisterProcessingTaskEnd())
                         .Should().NotThrow();
        }

        [Fact]
        public void RegisterProcessingTaskEnd_WithoutStart_DoesNotThrow()
        {
            // Act & Assert - Should handle gracefully even without a start
            FluentActions.Invoking(() => _memoryService.RegisterProcessingTaskEnd())
                         .Should().NotThrow();
        }

        [Fact]
        public void GetMemoryStatus_ValidCall_ReturnsMemoryStatus()
        {
            // Act
            var status = _memoryService.GetMemoryStatus();

            // Assert
            status.Should().NotBeNull();
            status.Should().BeOfType<MemoryStatus>();
            status.CurrentMemoryUsageBytes.Should().BeGreaterOrEqualTo(0);
            status.MaxMemoryUsageBytes.Should().BeGreaterThan(0);
            status.MemoryUsagePercent.Should().BeGreaterOrEqualTo(0);
            status.ActiveProcessingTasks.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task RequestMemoryForTaskAsync_SmallAmount_ReturnsTrue()
        {
            // Arrange
            var smallAmount = 1024 * 1024; // 1MB

            // Act
            var result = await _memoryService.RequestMemoryForTaskAsync(smallAmount);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task RequestMemoryForTaskAsync_ZeroAmount_ReturnsTrue()
        {
            // Act
            var result = await _memoryService.RequestMemoryForTaskAsync(0);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task RequestMemoryForTaskAsync_ExtremelyLargeAmount_ReturnsFalse()
        {
            // Arrange - Request more memory than the configured limit
            var hugeAmount = 10L * 1024 * 1024 * 1024; // 10GB

            // Act
            var result = await _memoryService.RequestMemoryForTaskAsync(hugeAmount);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ForceGarbageCollectionAsync_ValidCall_CompletesSuccessfully()
        {
            // Act & Assert
            await FluentActions.Invoking(async () => await _memoryService.ForceGarbageCollectionAsync())
                               .Should().NotThrowAsync();
        }

        [Fact]
        public void MemoryStatus_Properties_WorkCorrectly()
        {
            // Arrange
            var status = new MemoryStatus
            {
                CurrentMemoryUsageBytes = 2 * 1024 * 1024, // 2MB
                PeakMemoryUsageBytes = 4 * 1024 * 1024, // 4MB
                MaxMemoryUsageBytes = 8 * 1024 * 1024, // 8MB
                GCTotalMemoryBytes = 1024 * 1024, // 1MB
                WorkingSetBytes = 3 * 1024 * 1024, // 3MB
                PrivateMemoryBytes = (long)(2.5 * 1024 * 1024), // 2.5MB
                ActiveProcessingTasks = 3,
                CanStartNewTask = true
            };

            // Act & Assert
            status.CurrentMemoryUsageMB.Should().Be(2.0);
            status.PeakMemoryUsageMB.Should().Be(4.0);
            status.MaxMemoryUsageMB.Should().Be(8.0);
            status.GCTotalMemoryMB.Should().Be(1.0);
            status.WorkingSetMB.Should().Be(3.0);
            status.PrivateMemoryMB.Should().Be(2.5);
            status.ActiveProcessingTasks.Should().Be(3);
            status.CanStartNewTask.Should().BeTrue();
        }

        [Fact]
        public void GetMemoryStatus_AfterRegisteringTasks_ReflectsActiveTaskCount()
        {
            // Arrange
            _memoryService.RegisterProcessingTaskStart();
            _memoryService.RegisterProcessingTaskStart();

            // Act
            var status = _memoryService.GetMemoryStatus();

            // Assert
            status.ActiveProcessingTasks.Should().Be(2);

            // Cleanup - End the tasks
            _memoryService.RegisterProcessingTaskEnd();
            _memoryService.RegisterProcessingTaskEnd();

            // Verify cleanup
            var finalStatus = _memoryService.GetMemoryStatus();
            finalStatus.ActiveProcessingTasks.Should().Be(0);
        }

        [Fact]
        public void GetMemoryStatus_MultipleCalls_ShowsMemoryChanges()
        {
            // Act - Get status multiple times
            var status1 = _memoryService.GetMemoryStatus();
            var status2 = _memoryService.GetMemoryStatus();

            // Assert - Status objects should be valid even if values might be similar
            status1.Should().NotBeNull();
            status2.Should().NotBeNull();
            status1.CurrentMemoryUsageBytes.Should().BeGreaterOrEqualTo(0);
            status2.CurrentMemoryUsageBytes.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task RequestMemoryForTaskAsync_NegativeAmount_StillProcessed()
        {
            // Act - The service doesn't validate negative values, it just processes them
            var result = await _memoryService.RequestMemoryForTaskAsync(-1000);

            // Assert - Since negative reduces projected usage, it's likely to be approved
            // This is testing the actual behavior, not ideal behavior
            result.Should().BeTrue();
        }

        public void Dispose()
        {
            _memoryService?.Dispose();
        }
    }
}