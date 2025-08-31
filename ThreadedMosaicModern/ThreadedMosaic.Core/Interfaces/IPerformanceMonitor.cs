using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for performance monitoring and telemetry services
    /// </summary>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// Starts tracking a performance operation
        /// </summary>
        IOperationTracker StartOperation(string operationName, IDictionary<string, object>? metadata = null);

        /// <summary>
        /// Records a performance metric
        /// </summary>
        void RecordMetric(string metricName, double value, string? unit = null, IDictionary<string, object>? tags = null);

        /// <summary>
        /// Gets current system performance data
        /// </summary>
        SystemPerformanceData GetCurrentSystemPerformance();

        /// <summary>
        /// Gets performance statistics for a specific operation type
        /// </summary>
        OperationStatistics GetOperationStatistics(string operationName, TimeSpan? timeRange = null);

        /// <summary>
        /// Exports performance data to file
        /// </summary>
        Task<string> ExportPerformanceDataAsync(string filePath, TimeSpan? timeRange = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets performance insights and recommendations
        /// </summary>
        PerformanceInsights GetPerformanceInsights(TimeSpan? analysisTimeRange = null);
    }

    /// <summary>
    /// Interface for tracking individual operations
    /// </summary>
    public interface IOperationTracker : IDisposable
    {
        string OperationId { get; }
        string OperationName { get; }
        DateTime StartTime { get; }
        TimeSpan Duration { get; }
        IDictionary<string, object>? Metadata { get; }

        /// <summary>
        /// Records an intermediate metric for this operation
        /// </summary>
        void RecordIntermediateMetric(string metricName, double value, string? unit = null);

        /// <summary>
        /// Sets the throughput for this operation
        /// </summary>
        void SetThroughput(double itemsPerSecond);

        /// <summary>
        /// Marks the operation as successful or failed
        /// </summary>
        void SetResult(bool success, string? errorMessage = null);

        /// <summary>
        /// Adds or updates metadata for this operation
        /// </summary>
        void UpdateMetadata(string key, object value);

        /// <summary>
        /// Records progress for long-running operations
        /// </summary>
        void RecordProgress(double percentComplete, string? statusMessage = null);

        /// <summary>
        /// Records memory usage during the operation
        /// </summary>
        void RecordMemoryUsage(long bytesUsed);

        /// <summary>
        /// Records image processing specific metrics
        /// </summary>
        void RecordImageMetrics(int imageWidth, int imageHeight, long fileSizeBytes);

        /// <summary>
        /// Records file I/O statistics
        /// </summary>
        void RecordIOStats(long bytesRead, long bytesWritten);
    }

    /// <summary>
    /// System performance data snapshot
    /// </summary>
    public class SystemPerformanceData
    {
        public double CpuUsagePercent { get; set; }
        public long TotalMemoryMB { get; set; }
        public long AvailableMemoryMB { get; set; }
        public long UsedMemoryMB { get; set; }
        public double MemoryUsagePercent { get; set; }
        public Dictionary<string, DiskSpaceInfo> DiskSpaceInfo { get; set; } = new();
        public int ActiveProcesses { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Disk space information for a drive
    /// </summary>
    public class DiskSpaceInfo
    {
        public string DriveName { get; set; } = "";
        public double TotalSpaceGB { get; set; }
        public double AvailableSpaceGB { get; set; }
        public double UsedSpaceGB { get; set; }
        public double UsagePercent { get; set; }
    }

    /// <summary>
    /// Performance statistics for an operation type
    /// </summary>
    public class OperationStatistics
    {
        public string OperationName { get; set; } = "";
        public TimeSpan TimeRange { get; set; }
        public int SampleCount { get; set; }
        public double AverageDurationMs { get; set; }
        public double MinDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
        public double MedianDurationMs { get; set; }
        public double AverageThroughput { get; set; }
        public int TotalOperations { get; set; }
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Performance insights and recommendations
    /// </summary>
    public class PerformanceInsights
    {
        public DateTime GeneratedAt { get; set; }
        public TimeSpan AnalysisTimeRange { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public List<PerformanceTrend> PerformanceTrends { get; set; } = new();
        public ResourceUtilizationInsights ResourceUtilization { get; set; } = new();
    }

    /// <summary>
    /// Performance trend information
    /// </summary>
    public class PerformanceTrend
    {
        public string OperationName { get; set; } = "";
        public string MetricName { get; set; } = "";
        public TrendDirection TrendDirection { get; set; }
        public double ChangePercent { get; set; }
        public int SampleCount { get; set; }
    }

    /// <summary>
    /// Resource utilization insights
    /// </summary>
    public class ResourceUtilizationInsights
    {
        public double CpuEfficiency { get; set; }
        public double MemoryEfficiency { get; set; }
        public double IOEfficiency { get; set; }
        public int RecommendedConcurrency { get; set; }
    }

    /// <summary>
    /// Individual performance metric
    /// </summary>
    public class PerformanceMetric
    {
        public string Name { get; set; } = "";
        public double Value { get; set; }
        public string Unit { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public IDictionary<string, object> Tags { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// System information snapshot
    /// </summary>
    public class SystemInfo
    {
        public string MachineName { get; set; } = "";
        public string OperatingSystem { get; set; } = "";
        public int ProcessorCount { get; set; }
        public string RuntimeVersion { get; set; } = "";
        public string Architecture { get; set; } = "";
        public bool IsServerGC { get; set; }
        public DateTime CollectedAt { get; set; }
    }

    /// <summary>
    /// Performance data export structure
    /// </summary>
    public class PerformanceDataExport
    {
        public DateTime ExportTimestamp { get; set; }
        public TimeSpan TimeRange { get; set; }
        public SystemInfo SystemInfo { get; set; } = new();
        public SystemPerformanceData SystemPerformance { get; set; } = new();
        public List<PerformanceMetric> Metrics { get; set; } = new();
        public Dictionary<string, OperationStatistics> OperationSummaries { get; set; } = new();
    }

    /// <summary>
    /// Performance monitoring configuration options
    /// </summary>
    public class PerformanceMonitoringOptions
    {
        public static PerformanceMonitoringOptions Default => new()
        {
            SystemMetricsIntervalSeconds = 30,
            MaxMetricsInMemory = 10000,
            MetricRetentionHours = 24,
            CleanupIntervalMinutes = 60,
            EnablePeriodicSummary = true
        };

        public int SystemMetricsIntervalSeconds { get; set; } = 30;
        public int MaxMetricsInMemory { get; set; } = 10000;
        public int MetricRetentionHours { get; set; } = 24;
        public int CleanupIntervalMinutes { get; set; } = 60;
        public bool EnablePeriodicSummary { get; set; } = true;
    }

    /// <summary>
    /// Trend direction enumeration
    /// </summary>
    public enum TrendDirection
    {
        Stable,
        Increasing,
        Decreasing
    }
}