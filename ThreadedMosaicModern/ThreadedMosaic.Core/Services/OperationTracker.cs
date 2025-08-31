using System;
using System.Collections.Generic;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Implementation of IOperationTracker for tracking individual operation performance
    /// </summary>
    public class OperationTracker : IOperationTracker
    {
        private readonly PerformanceMonitoringService _monitoringService;
        private bool _disposed;

        public string OperationId { get; }
        public string OperationName { get; }
        public DateTime StartTime { get; }
        public TimeSpan Duration => DateTime.UtcNow - StartTime;
        public IDictionary<string, object>? Metadata { get; private set; }
        
        public bool? IsSuccessful { get; private set; }
        public string? ErrorMessage { get; private set; }
        public double? ThroughputItemsPerSecond { get; private set; }

        internal OperationTracker(
            string operationId, 
            string operationName, 
            IDictionary<string, object>? metadata,
            PerformanceMonitoringService monitoringService)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            Metadata = metadata;
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            StartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records an intermediate metric for this operation
        /// </summary>
        public void RecordIntermediateMetric(string metricName, double value, string? unit = null)
        {
            if (_disposed) return;

            var fullMetricName = $"{OperationName}_{metricName}";
            var tags = new Dictionary<string, object>
            {
                ["operation_id"] = OperationId,
                ["operation_name"] = OperationName
            };

            if (Metadata != null)
            {
                foreach (var kvp in Metadata)
                {
                    tags[$"meta_{kvp.Key}"] = kvp.Value;
                }
            }

            _monitoringService.RecordMetric(fullMetricName, value, unit, tags);
        }

        /// <summary>
        /// Sets the throughput for this operation
        /// </summary>
        public void SetThroughput(double itemsPerSecond)
        {
            if (_disposed) return;

            ThroughputItemsPerSecond = itemsPerSecond;
            RecordIntermediateMetric("throughput", itemsPerSecond, "items/sec");
        }

        /// <summary>
        /// Marks the operation as successful or failed
        /// </summary>
        public void SetResult(bool success, string? errorMessage = null)
        {
            if (_disposed) return;

            IsSuccessful = success;
            ErrorMessage = errorMessage;

            RecordIntermediateMetric("success", success ? 1 : 0, "boolean");
            
            if (!success && !string.IsNullOrEmpty(errorMessage))
            {
                UpdateMetadata("error_message", errorMessage);
            }
        }

        /// <summary>
        /// Adds or updates metadata for this operation
        /// </summary>
        public void UpdateMetadata(string key, object value)
        {
            if (_disposed) return;

            Metadata ??= new Dictionary<string, object>();
            Metadata[key] = value;
        }

        /// <summary>
        /// Records progress for long-running operations
        /// </summary>
        public void RecordProgress(double percentComplete, string? statusMessage = null)
        {
            if (_disposed) return;

            RecordIntermediateMetric("progress", percentComplete, "percent");
            
            if (!string.IsNullOrEmpty(statusMessage))
            {
                UpdateMetadata("current_status", statusMessage);
            }
        }

        /// <summary>
        /// Records memory usage during the operation
        /// </summary>
        public void RecordMemoryUsage(long bytesUsed)
        {
            if (_disposed) return;

            RecordIntermediateMetric("memory_usage", bytesUsed, "bytes");
        }

        /// <summary>
        /// Records the number of items processed
        /// </summary>
        public void RecordItemsProcessed(int itemCount)
        {
            if (_disposed) return;

            RecordIntermediateMetric("items_processed", itemCount, "count");
            
            // Calculate and update throughput if we have a meaningful duration
            var durationSeconds = Duration.TotalSeconds;
            if (durationSeconds > 0.1) // Only calculate after 100ms
            {
                SetThroughput(itemCount / durationSeconds);
            }
        }

        /// <summary>
        /// Records file I/O statistics
        /// </summary>
        public void RecordIOStats(long bytesRead, long bytesWritten)
        {
            if (_disposed) return;

            if (bytesRead > 0)
                RecordIntermediateMetric("bytes_read", bytesRead, "bytes");
                
            if (bytesWritten > 0)
                RecordIntermediateMetric("bytes_written", bytesWritten, "bytes");

            // Calculate I/O throughput
            var durationSeconds = Duration.TotalSeconds;
            if (durationSeconds > 0.1)
            {
                if (bytesRead > 0)
                    RecordIntermediateMetric("read_throughput", bytesRead / durationSeconds, "bytes/sec");
                    
                if (bytesWritten > 0)
                    RecordIntermediateMetric("write_throughput", bytesWritten / durationSeconds, "bytes/sec");
            }
        }

        /// <summary>
        /// Records CPU time used by this operation
        /// </summary>
        public void RecordCPUTime(TimeSpan cpuTime)
        {
            if (_disposed) return;

            RecordIntermediateMetric("cpu_time", cpuTime.TotalMilliseconds, "ms");
            
            // Calculate CPU efficiency (CPU time vs wall clock time)
            var wallClockTime = Duration.TotalMilliseconds;
            if (wallClockTime > 0)
            {
                var cpuEfficiency = (cpuTime.TotalMilliseconds / wallClockTime) * 100;
                RecordIntermediateMetric("cpu_efficiency", cpuEfficiency, "percent");
            }
        }

        /// <summary>
        /// Records image processing specific metrics
        /// </summary>
        public void RecordImageMetrics(int imageWidth, int imageHeight, long fileSizeBytes)
        {
            if (_disposed) return;

            RecordIntermediateMetric("image_width", imageWidth, "pixels");
            RecordIntermediateMetric("image_height", imageHeight, "pixels");
            RecordIntermediateMetric("image_pixels", imageWidth * imageHeight, "pixels");
            RecordIntermediateMetric("file_size", fileSizeBytes, "bytes");
            
            // Calculate pixels per second throughput
            var durationSeconds = Duration.TotalSeconds;
            if (durationSeconds > 0.1)
            {
                var pixelsPerSecond = (imageWidth * imageHeight) / durationSeconds;
                RecordIntermediateMetric("pixels_per_second", pixelsPerSecond, "pixels/sec");
            }
        }

        /// <summary>
        /// Creates a child operation tracker for sub-operations
        /// </summary>
        public IOperationTracker CreateChildOperation(string childOperationName, IDictionary<string, object>? childMetadata = null)
        {
            if (_disposed) 
                throw new ObjectDisposedException(nameof(OperationTracker));

            var combinedMetadata = new Dictionary<string, object>();
            
            // Include parent metadata
            if (Metadata != null)
            {
                foreach (var kvp in Metadata)
                {
                    combinedMetadata[$"parent_{kvp.Key}"] = kvp.Value;
                }
            }
            
            // Add parent operation info
            combinedMetadata["parent_operation_id"] = OperationId;
            combinedMetadata["parent_operation_name"] = OperationName;
            
            // Include child-specific metadata
            if (childMetadata != null)
            {
                foreach (var kvp in childMetadata)
                {
                    combinedMetadata[kvp.Key] = kvp.Value;
                }
            }

            return _monitoringService.StartOperation($"{OperationName}.{childOperationName}", combinedMetadata);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _monitoringService.CompleteOperation(this);
            }
        }
    }
}