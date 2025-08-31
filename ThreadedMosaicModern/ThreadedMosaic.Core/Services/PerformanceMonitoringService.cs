using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Comprehensive performance monitoring and telemetry service
    /// Tracks system resources, processing times, and provides performance insights
    /// </summary>
    public class PerformanceMonitoringService : BackgroundService, IPerformanceMonitor
    {
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
        private readonly ConcurrentDictionary<string, OperationTracker> _activeOperations;
        private readonly Timer _systemMetricsTimer;
        private readonly PerformanceMonitoringOptions _options;
        
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _memoryCounter;
        private Process _currentProcess;

        public PerformanceMonitoringService(
            ILogger<PerformanceMonitoringService> logger,
            PerformanceMonitoringOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? PerformanceMonitoringOptions.Default;
            _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
            _activeOperations = new ConcurrentDictionary<string, OperationTracker>();
            _currentProcess = Process.GetCurrentProcess();

            InitializePerformanceCounters();
            
            _systemMetricsTimer = new Timer(CollectSystemMetrics, null, 
                TimeSpan.Zero, TimeSpan.FromSeconds(_options.SystemMetricsIntervalSeconds));
        }

        /// <summary>
        /// Starts tracking a performance operation
        /// </summary>
        public IOperationTracker StartOperation(string operationName, IDictionary<string, object>? metadata = null)
        {
            var operationId = Guid.NewGuid().ToString("N");
            var tracker = new OperationTracker(operationId, operationName, metadata, this);
            
            _activeOperations.TryAdd(operationId, tracker);
            
            _logger.LogDebug("Started tracking operation: {OperationName} ({OperationId})", operationName, operationId);
            
            return tracker;
        }

        /// <summary>
        /// Records a performance metric
        /// </summary>
        public void RecordMetric(string metricName, double value, string? unit = null, IDictionary<string, object>? tags = null)
        {
            var timestamp = DateTime.UtcNow;
            var metric = new PerformanceMetric
            {
                Name = metricName,
                Value = value,
                Unit = unit ?? "count",
                Timestamp = timestamp,
                Tags = tags ?? new Dictionary<string, object>()
            };

            _metrics.AddOrUpdate($"{metricName}_{timestamp.Ticks}", metric, (key, existing) => metric);
            
            // Keep only recent metrics to prevent memory buildup
            if (_metrics.Count > _options.MaxMetricsInMemory)
            {
                var oldestKey = _metrics.Keys.OrderBy(k => k).First();
                _metrics.TryRemove(oldestKey, out _);
            }

            _logger.LogTrace("Recorded metric: {MetricName} = {Value} {Unit}", metricName, value, metric.Unit);
        }

        /// <summary>
        /// Gets current system performance data
        /// </summary>
        public SystemPerformanceData GetCurrentSystemPerformance()
        {
            try
            {
                var cpuUsage = GetCpuUsage();
                var memoryInfo = GetMemoryInfo();
                var diskInfo = GetDiskInfo();

                return new SystemPerformanceData
                {
                    CpuUsagePercent = cpuUsage,
                    TotalMemoryMB = memoryInfo.Total,
                    AvailableMemoryMB = memoryInfo.Available,
                    UsedMemoryMB = memoryInfo.Used,
                    MemoryUsagePercent = memoryInfo.UsagePercent,
                    DiskSpaceInfo = diskInfo,
                    ActiveProcesses = Process.GetProcesses().Length,
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect system performance data");
                return new SystemPerformanceData { Timestamp = DateTime.UtcNow };
            }
        }

        /// <summary>
        /// Gets performance statistics for a specific operation type
        /// </summary>
        public OperationStatistics GetOperationStatistics(string operationName, TimeSpan? timeRange = null)
        {
            var cutoffTime = timeRange.HasValue ? DateTime.UtcNow - timeRange.Value : DateTime.MinValue;
            
            var relevantMetrics = _metrics.Values
                .Where(m => m.Name.StartsWith(operationName) && m.Timestamp >= cutoffTime)
                .ToList();

            if (!relevantMetrics.Any())
            {
                return new OperationStatistics
                {
                    OperationName = operationName,
                    TimeRange = timeRange ?? TimeSpan.Zero,
                    SampleCount = 0
                };
            }

            var durations = relevantMetrics
                .Where(m => m.Name.EndsWith("_duration") && m.Unit == "ms")
                .Select(m => m.Value)
                .ToList();

            var throughputs = relevantMetrics
                .Where(m => m.Name.EndsWith("_throughput"))
                .Select(m => m.Value)
                .ToList();

            return new OperationStatistics
            {
                OperationName = operationName,
                TimeRange = timeRange ?? TimeSpan.Zero,
                SampleCount = durations.Count,
                AverageDurationMs = durations.Any() ? durations.Average() : 0,
                MinDurationMs = durations.Any() ? durations.Min() : 0,
                MaxDurationMs = durations.Any() ? durations.Max() : 0,
                MedianDurationMs = durations.Any() ? CalculateMedian(durations) : 0,
                AverageThroughput = throughputs.Any() ? throughputs.Average() : 0,
                TotalOperations = relevantMetrics.Count(m => m.Name.EndsWith("_count")),
                SuccessRate = CalculateSuccessRate(relevantMetrics)
            };
        }

        /// <summary>
        /// Exports performance data to JSON file
        /// </summary>
        public async Task<string> ExportPerformanceDataAsync(string filePath, TimeSpan? timeRange = null, CancellationToken cancellationToken = default)
        {
            var cutoffTime = timeRange.HasValue ? DateTime.UtcNow - timeRange.Value : DateTime.MinValue;
            
            var exportData = new PerformanceDataExport
            {
                ExportTimestamp = DateTime.UtcNow,
                TimeRange = timeRange ?? TimeSpan.Zero,
                SystemInfo = GetSystemInfo(),
                SystemPerformance = GetCurrentSystemPerformance(),
                Metrics = _metrics.Values
                    .Where(m => m.Timestamp >= cutoffTime)
                    .OrderBy(m => m.Timestamp)
                    .ToList(),
                OperationSummaries = GetAllOperationSummaries(timeRange)
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var jsonContent = JsonSerializer.Serialize(exportData, jsonOptions);
            await File.WriteAllTextAsync(filePath, jsonContent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Exported performance data to: {FilePath} ({MetricCount} metrics)", 
                filePath, exportData.Metrics.Count);

            return filePath;
        }

        /// <summary>
        /// Gets performance insights and recommendations
        /// </summary>
        public PerformanceInsights GetPerformanceInsights(TimeSpan? analysisTimeRange = null)
        {
            var insights = new PerformanceInsights
            {
                GeneratedAt = DateTime.UtcNow,
                AnalysisTimeRange = analysisTimeRange ?? TimeSpan.FromHours(1)
            };

            var systemPerf = GetCurrentSystemPerformance();
            var recentOperations = GetRecentOperationStatistics(analysisTimeRange);

            // CPU Analysis
            if (systemPerf.CpuUsagePercent > 80)
            {
                insights.Recommendations.Add("High CPU usage detected. Consider optimizing image processing algorithms or reducing concurrent operations.");
            }

            // Memory Analysis  
            if (systemPerf.MemoryUsagePercent > 85)
            {
                insights.Recommendations.Add("High memory usage detected. Consider implementing more aggressive caching policies or memory-mapped file processing for large images.");
            }

            // Operation Performance Analysis
            foreach (var operation in recentOperations)
            {
                if (operation.AverageDurationMs > 30000) // 30 seconds
                {
                    insights.Recommendations.Add($"Operation '{operation.OperationName}' has high average duration ({operation.AverageDurationMs:F0}ms). Consider optimization or preprocessing.");
                }

                if (operation.SuccessRate < 0.95) // Less than 95%
                {
                    insights.Recommendations.Add($"Operation '{operation.OperationName}' has low success rate ({operation.SuccessRate:P}). Investigate error causes.");
                }
            }

            // Performance Trends
            insights.PerformanceTrends = AnalyzePerformanceTrends(analysisTimeRange);

            // Resource Utilization
            insights.ResourceUtilization = new ResourceUtilizationInsights
            {
                CpuEfficiency = CalculateCpuEfficiency(recentOperations),
                MemoryEfficiency = CalculateMemoryEfficiency(),
                IOEfficiency = CalculateIOEfficiency(recentOperations),
                RecommendedConcurrency = CalculateRecommendedConcurrency(systemPerf)
            };

            return insights;
        }

        internal void CompleteOperation(OperationTracker tracker)
        {
            _activeOperations.TryRemove(tracker.OperationId, out _);

            // Record completion metrics
            var duration = tracker.Duration.TotalMilliseconds;
            RecordMetric($"{tracker.OperationName}_duration", duration, "ms", tracker.Metadata);
            RecordMetric($"{tracker.OperationName}_count", 1, "count");

            if (tracker.IsSuccessful.HasValue)
            {
                RecordMetric($"{tracker.OperationName}_success", tracker.IsSuccessful.Value ? 1 : 0, "boolean");
            }

            if (tracker.ThroughputItemsPerSecond.HasValue)
            {
                RecordMetric($"{tracker.OperationName}_throughput", tracker.ThroughputItemsPerSecond.Value, "items/sec");
            }

            _logger.LogDebug("Completed tracking operation: {OperationName} in {Duration:F2}ms", 
                tracker.OperationName, duration);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Periodic cleanup of old metrics
                    await CleanupOldMetricsAsync();
                    
                    // Log performance summary periodically
                    if (_options.EnablePeriodicSummary)
                    {
                        LogPerformanceSummary();
                    }

                    await Task.Delay(TimeSpan.FromMinutes(_options.CleanupIntervalMinutes), stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error in performance monitoring background task");
                }
            }
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                }
                _logger.LogDebug("Performance counters initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize performance counters, using fallback methods");
            }
        }

        private void CollectSystemMetrics(object? state)
        {
            try
            {
                var systemPerf = GetCurrentSystemPerformance();
                
                RecordMetric("system_cpu_usage", systemPerf.CpuUsagePercent, "percent");
                RecordMetric("system_memory_usage", systemPerf.MemoryUsagePercent, "percent");
                RecordMetric("system_available_memory", systemPerf.AvailableMemoryMB, "MB");
                RecordMetric("system_thread_count", systemPerf.ThreadCount, "count");
                RecordMetric("system_handle_count", systemPerf.HandleCount, "count");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect system metrics");
            }
        }

        private double GetCpuUsage()
        {
            try
            {
                if (_cpuCounter != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return _cpuCounter.NextValue();
                }
                
                // Fallback: Use process CPU time calculation
                var startTime = DateTime.UtcNow;
                var startCpuUsage = _currentProcess.TotalProcessorTime;
                
                Thread.Sleep(100); // Short delay for measurement
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = _currentProcess.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                return Math.Min(100, cpuUsageTotal * 100);
            }
            catch
            {
                return 0; // Fallback value
            }
        }

        private (long Total, long Available, long Used, double UsagePercent) GetMemoryInfo()
        {
            try
            {
                var totalMemory = GC.GetTotalMemory(false);
                var workingSet = _currentProcess.WorkingSet64;
                
                if (_memoryCounter != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var availableMB = _memoryCounter.NextValue();
                    var availableBytes = (long)(availableMB * 1024 * 1024);
                    var totalSystemMemory = availableBytes + workingSet; // Approximation
                    
                    return (
                        Total: totalSystemMemory / (1024 * 1024),
                        Available: (long)availableMB,
                        Used: workingSet / (1024 * 1024),
                        UsagePercent: (double)workingSet / totalSystemMemory * 100
                    );
                }
                
                // Fallback for non-Windows systems
                var processMemoryMB = workingSet / (1024 * 1024);
                return (
                    Total: processMemoryMB * 2, // Rough estimate
                    Available: processMemoryMB,
                    Used: processMemoryMB,
                    UsagePercent: 50 // Rough estimate
                );
            }
            catch
            {
                return (0, 0, 0, 0);
            }
        }

        private Dictionary<string, DiskSpaceInfo> GetDiskInfo()
        {
            var diskInfo = new Dictionary<string, DiskSpaceInfo>();
            
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                
                foreach (var drive in drives)
                {
                    var totalSizeGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    var availableGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    var usedGB = totalSizeGB - availableGB;
                    
                    diskInfo[drive.Name] = new DiskSpaceInfo
                    {
                        DriveName = drive.Name,
                        TotalSpaceGB = totalSizeGB,
                        AvailableSpaceGB = availableGB,
                        UsedSpaceGB = usedGB,
                        UsagePercent = totalSizeGB > 0 ? (usedGB / totalSizeGB) * 100 : 0
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect disk information");
            }
            
            return diskInfo;
        }

        private SystemInfo GetSystemInfo()
        {
            return new SystemInfo
            {
                MachineName = Environment.MachineName,
                OperatingSystem = RuntimeInformation.OSDescription,
                ProcessorCount = Environment.ProcessorCount,
                RuntimeVersion = RuntimeInformation.FrameworkDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                IsServerGC = GCSettings.IsServerGC,
                CollectedAt = DateTime.UtcNow
            };
        }

        private double CalculateMedian(List<double> values)
        {
            var sorted = values.OrderBy(x => x).ToList();
            int count = sorted.Count;
            
            if (count == 0) return 0;
            if (count % 2 == 0)
            {
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            }
            else
            {
                return sorted[count / 2];
            }
        }

        private double CalculateSuccessRate(List<PerformanceMetric> metrics)
        {
            var successMetrics = metrics.Where(m => m.Name.EndsWith("_success") && m.Unit == "boolean").ToList();
            
            if (!successMetrics.Any()) return 1.0; // Assume success if no failure metrics
            
            var totalOperations = successMetrics.Count;
            var successfulOperations = successMetrics.Count(m => m.Value > 0);
            
            return totalOperations > 0 ? (double)successfulOperations / totalOperations : 1.0;
        }

        private List<OperationStatistics> GetRecentOperationStatistics(TimeSpan? timeRange)
        {
            var range = timeRange ?? TimeSpan.FromHours(1);
            var operationNames = _metrics.Values
                .Where(m => m.Timestamp >= DateTime.UtcNow - range)
                .Select(m => m.Name.Split('_')[0])
                .Distinct()
                .ToList();

            return operationNames.Select(name => GetOperationStatistics(name, range)).ToList();
        }

        private Dictionary<string, OperationStatistics> GetAllOperationSummaries(TimeSpan? timeRange)
        {
            var result = new Dictionary<string, OperationStatistics>();
            var range = timeRange ?? TimeSpan.FromDays(1);
            
            var operationNames = _metrics.Values
                .Where(m => m.Timestamp >= DateTime.UtcNow - range)
                .Select(m => m.Name.Split('_')[0])
                .Distinct();

            foreach (var operationName in operationNames)
            {
                result[operationName] = GetOperationStatistics(operationName, range);
            }

            return result;
        }

        private List<PerformanceTrend> AnalyzePerformanceTrends(TimeSpan? analysisTimeRange)
        {
            var trends = new List<PerformanceTrend>();
            var range = analysisTimeRange ?? TimeSpan.FromHours(1);
            var cutoffTime = DateTime.UtcNow - range;

            var recentMetrics = _metrics.Values
                .Where(m => m.Timestamp >= cutoffTime)
                .GroupBy(m => m.Name.Split('_')[0])
                .ToList();

            foreach (var group in recentMetrics)
            {
                var operationName = group.Key;
                var durationMetrics = group
                    .Where(m => m.Name.EndsWith("_duration"))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                if (durationMetrics.Count >= 2)
                {
                    var firstHalf = durationMetrics.Take(durationMetrics.Count / 2);
                    var secondHalf = durationMetrics.Skip(durationMetrics.Count / 2);

                    var firstAvg = firstHalf.Average(m => m.Value);
                    var secondAvg = secondHalf.Average(m => m.Value);

                    var trendDirection = Math.Abs(secondAvg - firstAvg) < firstAvg * 0.1 
                        ? TrendDirection.Stable
                        : secondAvg > firstAvg ? TrendDirection.Increasing : TrendDirection.Decreasing;

                    trends.Add(new PerformanceTrend
                    {
                        OperationName = operationName,
                        MetricName = "Duration",
                        TrendDirection = trendDirection,
                        ChangePercent = firstAvg > 0 ? ((secondAvg - firstAvg) / firstAvg) * 100 : 0,
                        SampleCount = durationMetrics.Count
                    });
                }
            }

            return trends;
        }

        private double CalculateCpuEfficiency(List<OperationStatistics> operations)
        {
            if (!operations.Any()) return 1.0;

            var averageDuration = operations.Average(o => o.AverageDurationMs);
            var averageThroughput = operations.Average(o => o.AverageThroughput);

            // Simple efficiency calculation: higher throughput with lower duration = higher efficiency
            return averageDuration > 0 ? Math.Min(1.0, averageThroughput / averageDuration * 1000) : 1.0;
        }

        private double CalculateMemoryEfficiency()
        {
            var currentMemory = GetCurrentSystemPerformance();
            return Math.Max(0, 1.0 - (currentMemory.MemoryUsagePercent / 100.0));
        }

        private double CalculateIOEfficiency(List<OperationStatistics> operations)
        {
            if (!operations.Any()) return 1.0;

            var imageProcessingOps = operations.Where(o => 
                o.OperationName.Contains("Image") || o.OperationName.Contains("Mosaic")).ToList();

            if (!imageProcessingOps.Any()) return 1.0;

            var avgThroughput = imageProcessingOps.Average(o => o.AverageThroughput);
            return Math.Min(1.0, avgThroughput / 100); // Normalize to reasonable scale
        }

        private int CalculateRecommendedConcurrency(SystemPerformanceData systemPerf)
        {
            var baseConcurrency = Environment.ProcessorCount;
            
            // Reduce concurrency if CPU or memory usage is high
            if (systemPerf.CpuUsagePercent > 80) baseConcurrency = Math.Max(1, baseConcurrency / 2);
            if (systemPerf.MemoryUsagePercent > 85) baseConcurrency = Math.Max(1, baseConcurrency / 2);
            
            return baseConcurrency;
        }

        private async Task CleanupOldMetricsAsync()
        {
            var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(_options.MetricRetentionHours);
            var oldMetrics = _metrics.Where(kvp => kvp.Value.Timestamp < cutoffTime).Select(kvp => kvp.Key).ToList();
            
            foreach (var key in oldMetrics)
            {
                _metrics.TryRemove(key, out _);
            }
            
            if (oldMetrics.Any())
            {
                _logger.LogDebug("Cleaned up {Count} old performance metrics", oldMetrics.Count);
            }
            
            await Task.CompletedTask;
        }

        private void LogPerformanceSummary()
        {
            var systemPerf = GetCurrentSystemPerformance();
            var activeOpsCount = _activeOperations.Count;
            var totalMetrics = _metrics.Count;

            _logger.LogInformation("Performance Summary - CPU: {Cpu:F1}%, Memory: {Memory:F1}%, Active Operations: {ActiveOps}, Total Metrics: {Metrics}",
                systemPerf.CpuUsagePercent, systemPerf.MemoryUsagePercent, activeOpsCount, totalMetrics);
        }

        public override void Dispose()
        {
            _systemMetricsTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            _currentProcess?.Dispose();
            base.Dispose();
        }
    }
}