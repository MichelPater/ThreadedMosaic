using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ThreadedMosaic.Core.Services;

namespace ThreadedMosaic.Tests.Services
{
    /// <summary>
    /// Tests for MosaicCancellationService to verify cancellation tracking functionality
    /// </summary>
    public class MosaicCancellationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<MosaicCancellationService>> _mockLogger;
        private readonly MosaicCancellationService _cancellationService;

        public MosaicCancellationServiceTests()
        {
            _mockLogger = new Mock<ILogger<MosaicCancellationService>>();
            _cancellationService = new MosaicCancellationService(_mockLogger.Object);
        }

        [Fact]
        public void RegisterOperation_NewOperation_ReturnsValidToken()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();

            // Act
            var token = _cancellationService.RegisterOperation(mosaicId);

            // Assert
            token.Should().NotBe(default);
            token.IsCancellationRequested.Should().BeFalse();
            _cancellationService.IsOperationActive(mosaicId).Should().BeTrue();
        }

        [Fact]
        public void RegisterOperation_DuplicateOperation_ReturnsExistingToken()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();

            // Act
            var token1 = _cancellationService.RegisterOperation(mosaicId);
            var token2 = _cancellationService.RegisterOperation(mosaicId);

            // Assert
            token1.Should().Be(token2);
            _cancellationService.ActiveOperationsCount.Should().Be(1);
        }

        [Fact]
        public void CancelOperation_ExistingOperation_ReturnsTrueAndCancelsToken()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();
            var token = _cancellationService.RegisterOperation(mosaicId);

            // Act
            var result = _cancellationService.CancelOperation(mosaicId);

            // Assert
            result.Should().BeTrue();
            token.IsCancellationRequested.Should().BeTrue();
            _cancellationService.IsOperationCancelled(mosaicId).Should().BeTrue();
        }

        [Fact]
        public void CancelOperation_NonExistentOperation_ReturnsFalse()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();

            // Act
            var result = _cancellationService.CancelOperation(mosaicId);

            // Assert
            result.Should().BeFalse();
            _cancellationService.IsOperationActive(mosaicId).Should().BeFalse();
        }

        [Fact]
        public void CancelOperation_AlreadyCancelledOperation_ReturnsTrue()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();
            _cancellationService.RegisterOperation(mosaicId);
            _cancellationService.CancelOperation(mosaicId);

            // Act
            var result = _cancellationService.CancelOperation(mosaicId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void UnregisterOperation_ExistingOperation_RemovesOperation()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();
            _cancellationService.RegisterOperation(mosaicId);

            // Act
            _cancellationService.UnregisterOperation(mosaicId);

            // Assert
            _cancellationService.IsOperationActive(mosaicId).Should().BeFalse();
            _cancellationService.ActiveOperationsCount.Should().Be(0);
        }

        [Fact]
        public void UnregisterOperation_NonExistentOperation_DoesNotThrow()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();

            // Act & Assert
            FluentActions.Invoking(() => _cancellationService.UnregisterOperation(mosaicId))
                         .Should().NotThrow();
        }

        [Fact]
        public void IsOperationActive_ExistingOperation_ReturnsTrue()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();
            _cancellationService.RegisterOperation(mosaicId);

            // Act
            var result = _cancellationService.IsOperationActive(mosaicId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsOperationActive_NonExistentOperation_ReturnsFalse()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();

            // Act
            var result = _cancellationService.IsOperationActive(mosaicId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsOperationCancelled_CancelledOperation_ReturnsTrue()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();
            _cancellationService.RegisterOperation(mosaicId);
            _cancellationService.CancelOperation(mosaicId);

            // Act
            var result = _cancellationService.IsOperationCancelled(mosaicId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsOperationCancelled_ActiveOperation_ReturnsFalse()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();
            _cancellationService.RegisterOperation(mosaicId);

            // Act
            var result = _cancellationService.IsOperationCancelled(mosaicId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsOperationCancelled_NonExistentOperation_ReturnsFalse()
        {
            // Arrange
            var mosaicId = Guid.NewGuid();

            // Act
            var result = _cancellationService.IsOperationCancelled(mosaicId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ActiveOperationsCount_MultipleOperations_ReturnsCorrectCount()
        {
            // Arrange
            var mosaicId1 = Guid.NewGuid();
            var mosaicId2 = Guid.NewGuid();
            var mosaicId3 = Guid.NewGuid();

            // Act
            _cancellationService.RegisterOperation(mosaicId1);
            _cancellationService.RegisterOperation(mosaicId2);
            _cancellationService.RegisterOperation(mosaicId3);

            // Assert
            _cancellationService.ActiveOperationsCount.Should().Be(3);

            // Act - Remove one
            _cancellationService.UnregisterOperation(mosaicId2);

            // Assert
            _cancellationService.ActiveOperationsCount.Should().Be(2);
        }

        [Fact]
        public void CancelAllOperations_MultipleOperations_CancelsAll()
        {
            // Arrange
            var mosaicId1 = Guid.NewGuid();
            var mosaicId2 = Guid.NewGuid();
            var token1 = _cancellationService.RegisterOperation(mosaicId1);
            var token2 = _cancellationService.RegisterOperation(mosaicId2);

            // Act
            _cancellationService.CancelAllOperations();

            // Assert
            token1.IsCancellationRequested.Should().BeTrue();
            token2.IsCancellationRequested.Should().BeTrue();
            _cancellationService.ActiveOperationsCount.Should().Be(0);
        }

        [Fact]
        public void Dispose_WithActiveOperations_CancelsAllOperations()
        {
            // Arrange
            var mosaicId1 = Guid.NewGuid();
            var mosaicId2 = Guid.NewGuid();
            var token1 = _cancellationService.RegisterOperation(mosaicId1);
            var token2 = _cancellationService.RegisterOperation(mosaicId2);

            // Act
            _cancellationService.Dispose();

            // Assert
            token1.IsCancellationRequested.Should().BeTrue();
            token2.IsCancellationRequested.Should().BeTrue();
        }

        public void Dispose()
        {
            _cancellationService?.Dispose();
        }
    }
}