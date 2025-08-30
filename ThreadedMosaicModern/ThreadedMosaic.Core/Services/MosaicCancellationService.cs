using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Service for tracking and managing cancellation of active mosaic operations
    /// </summary>
    public class MosaicCancellationService : IMosaicCancellationService
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeOperations;
        private readonly ILogger<MosaicCancellationService> _logger;

        public MosaicCancellationService(ILogger<MosaicCancellationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeOperations = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        }

        /// <summary>
        /// Registers a new mosaic operation for tracking
        /// </summary>
        /// <param name="mosaicId">Unique identifier for the mosaic operation</param>
        /// <returns>CancellationToken that can be used to cancel the operation</returns>
        public CancellationToken RegisterOperation(Guid mosaicId)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            
            if (_activeOperations.TryAdd(mosaicId, cancellationTokenSource))
            {
                _logger.LogInformation("Registered mosaic operation: {MosaicId}", mosaicId);
                return cancellationTokenSource.Token;
            }
            
            // If operation already exists, dispose the new one and return existing
            cancellationTokenSource.Dispose();
            var existingSource = _activeOperations[mosaicId];
            _logger.LogWarning("Operation {MosaicId} was already registered", mosaicId);
            return existingSource.Token;
        }

        /// <summary>
        /// Requests cancellation of a mosaic operation
        /// </summary>
        /// <param name="mosaicId">Unique identifier for the mosaic operation</param>
        /// <returns>True if the operation was found and cancellation was requested, false otherwise</returns>
        public bool CancelOperation(Guid mosaicId)
        {
            if (_activeOperations.TryGetValue(mosaicId, out var cancellationTokenSource))
            {
                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancelling mosaic operation: {MosaicId}", mosaicId);
                    cancellationTokenSource.Cancel();
                    return true;
                }
                
                _logger.LogInformation("Operation {MosaicId} was already cancelled", mosaicId);
                return true;
            }

            _logger.LogWarning("Cannot cancel operation {MosaicId} - not found", mosaicId);
            return false;
        }

        /// <summary>
        /// Checks if an operation is currently being tracked
        /// </summary>
        /// <param name="mosaicId">Unique identifier for the mosaic operation</param>
        /// <returns>True if the operation is being tracked, false otherwise</returns>
        public bool IsOperationActive(Guid mosaicId)
        {
            return _activeOperations.ContainsKey(mosaicId);
        }

        /// <summary>
        /// Checks if an operation has been cancelled
        /// </summary>
        /// <param name="mosaicId">Unique identifier for the mosaic operation</param>
        /// <returns>True if the operation has been cancelled, false if not cancelled or not found</returns>
        public bool IsOperationCancelled(Guid mosaicId)
        {
            if (_activeOperations.TryGetValue(mosaicId, out var cancellationTokenSource))
            {
                return cancellationTokenSource.Token.IsCancellationRequested;
            }
            return false;
        }

        /// <summary>
        /// Unregisters a completed or failed mosaic operation
        /// </summary>
        /// <param name="mosaicId">Unique identifier for the mosaic operation</param>
        public void UnregisterOperation(Guid mosaicId)
        {
            if (_activeOperations.TryRemove(mosaicId, out var cancellationTokenSource))
            {
                _logger.LogInformation("Unregistered mosaic operation: {MosaicId}", mosaicId);
                cancellationTokenSource.Dispose();
            }
            else
            {
                _logger.LogWarning("Cannot unregister operation {MosaicId} - not found", mosaicId);
            }
        }

        /// <summary>
        /// Gets the number of currently active operations
        /// </summary>
        public int ActiveOperationsCount => _activeOperations.Count;

        /// <summary>
        /// Cancels all active operations (useful for application shutdown)
        /// </summary>
        public void CancelAllOperations()
        {
            _logger.LogInformation("Cancelling all {Count} active operations", _activeOperations.Count);
            
            foreach (var kvp in _activeOperations)
            {
                try
                {
                    if (!kvp.Value.Token.IsCancellationRequested)
                    {
                        kvp.Value.Cancel();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling operation {MosaicId}", kvp.Key);
                }
            }
            
            // Clear and dispose all
            var operations = _activeOperations.ToArray();
            _activeOperations.Clear();
            
            foreach (var kvp in operations)
            {
                try
                {
                    kvp.Value.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing cancellation token for operation {MosaicId}", kvp.Key);
                }
            }
        }

        /// <summary>
        /// Disposes all resources
        /// </summary>
        public void Dispose()
        {
            CancelAllOperations();
        }
    }

    /// <summary>
    /// Interface for mosaic cancellation service
    /// </summary>
    public interface IMosaicCancellationService : IDisposable
    {
        CancellationToken RegisterOperation(Guid mosaicId);
        bool CancelOperation(Guid mosaicId);
        bool IsOperationActive(Guid mosaicId);
        bool IsOperationCancelled(Guid mosaicId);
        void UnregisterOperation(Guid mosaicId);
        int ActiveOperationsCount { get; }
        void CancelAllOperations();
    }
}