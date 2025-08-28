using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Service for monitoring and managing memory usage during image processing operations
    /// </summary>
    public class MemoryManagementService : BackgroundService
    {
        private readonly ILogger<MemoryManagementService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _monitoringInterval;
        private readonly long _maxMemoryUsageBytes;
        private readonly double _memoryWarningThreshold;
        private readonly double _memoryCriticalThreshold;
        private readonly bool _enableAutomaticGarbageCollection;
        private readonly bool _enableMemoryPressureOptimization;

        private long _currentMemoryUsage;
        private long _peakMemoryUsage;
        private int _activeProcessingTasks;
        private readonly object _lockObject = new object();

        public MemoryManagementService(
            ILogger<MemoryManagementService> logger, 
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

            // Load configuration settings
            _monitoringInterval = TimeSpan.FromSeconds(_configuration.GetValue<int>("MosaicConfiguration:Performance:ResourceMonitoringIntervalSeconds", 5));
            _maxMemoryUsageBytes = _configuration.GetValue<long>("MosaicConfiguration:Performance:MaxMemoryUsageMB", 1024) * 1024 * 1024;
            _memoryWarningThreshold = _configuration.GetValue<double>("MosaicConfiguration:Performance:MemoryWarningThreshold", 0.75);
            _memoryCriticalThreshold = _configuration.GetValue<double>("MosaicConfiguration:Performance:MemoryCriticalThreshold", 0.90);
            _enableAutomaticGarbageCollection = _configuration.GetValue<bool>("MosaicConfiguration:Performance:EnableAutomaticGarbageCollection", true);
            _enableMemoryPressureOptimization = _configuration.GetValue<bool>("MosaicConfiguration:Performance:EnableMemoryPressureOptimization", true);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MemoryManagementService started. Monitoring interval: {Interval}, Max memory: {MaxMemoryMB} MB", 
                _monitoringInterval, _maxMemoryUsageBytes / 1024 / 1024);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorMemoryUsageAsync();
                    await Task.Delay(_monitoringInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("MemoryManagementService stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during memory monitoring cycle");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }

        private async Task MonitorMemoryUsageAsync()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var workingSetBytes = process.WorkingSet64;
                var privateBytesBytes = process.PrivateMemorySize64;
                var gcTotalMemory = GC.GetTotalMemory(false);

                lock (_lockObject)
                {
                    _currentMemoryUsage = Math.Max(workingSetBytes, privateBytesBytes);
                    _peakMemoryUsage = Math.Max(_peakMemoryUsage, _currentMemoryUsage);
                }

                var memoryUsagePercent = (double)_currentMemoryUsage / _maxMemoryUsageBytes;

                _logger.LogTrace("Memory usage: {CurrentMB:F2} MB / {MaxMB} MB ({Percent:P1}) | GC: {GCMB:F2} MB | Active tasks: {ActiveTasks}",
                    _currentMemoryUsage / 1024.0 / 1024.0,
                    _maxMemoryUsageBytes / 1024 / 1024,
                    memoryUsagePercent,
                    gcTotalMemory / 1024.0 / 1024.0,
                    _activeProcessingTasks);

                // Check memory thresholds and take action
                if (memoryUsagePercent >= _memoryCriticalThreshold)
                {
                    await HandleCriticalMemoryUsageAsync();
                }
                else if (memoryUsagePercent >= _memoryWarningThreshold)
                {
                    await HandleHighMemoryUsageAsync();
                }

                // Memory pressure optimization
                if (_enableMemoryPressureOptimization)
                {
                    await OptimizeMemoryPressureAsync(memoryUsagePercent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring memory usage");
            }

            await Task.CompletedTask;
        }

        private async Task HandleHighMemoryUsageAsync()
        {
            _logger.LogWarning("High memory usage detected: {CurrentMB:F2} MB / {MaxMB} MB ({Percent:P1})",
                _currentMemoryUsage / 1024.0 / 1024.0,
                _maxMemoryUsageBytes / 1024 / 1024,
                (double)_currentMemoryUsage / _maxMemoryUsageBytes);

            // Clear some cache entries
            if (_memoryCache is MemoryCache mc)
            {
                mc.Compact(0.25); // Remove 25% of cache entries
                _logger.LogDebug("Cache compacted due to high memory usage");
            }

            // Suggest garbage collection
            if (_enableAutomaticGarbageCollection)
            {
                GC.Collect(1, GCCollectionMode.Optimized);
                _logger.LogDebug("Generation 1 garbage collection performed");
            }

            await Task.CompletedTask;
        }

        private async Task HandleCriticalMemoryUsageAsync()
        {
            _logger.LogError("Critical memory usage detected: {CurrentMB:F2} MB / {MaxMB} MB ({Percent:P1})",
                _currentMemoryUsage / 1024.0 / 1024.0,
                _maxMemoryUsageBytes / 1024 / 1024,
                (double)_currentMemoryUsage / _maxMemoryUsageBytes);

            // Aggressive cache clearing
            if (_memoryCache is MemoryCache mc)
            {
                mc.Compact(1.0); // Clear entire cache
                _logger.LogWarning("Cache completely cleared due to critical memory usage");
            }

            // Force full garbage collection
            if (_enableAutomaticGarbageCollection)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                _logger.LogWarning("Full garbage collection performed due to critical memory usage");
            }

            // TODO: Consider pausing new processing tasks or cancelling existing ones
            _logger.LogWarning("Consider reducing concurrent processing tasks. Current active tasks: {ActiveTasks}", _activeProcessingTasks);

            await Task.CompletedTask;
        }

        private async Task OptimizeMemoryPressureAsync(double memoryUsagePercent)
        {
            // Adjust GC latency mode based on memory pressure
            if (memoryUsagePercent > 0.8)
            {
                if (GCSettings.LatencyMode != GCLatencyMode.LowLatency)
                {
                    GCSettings.LatencyMode = GCLatencyMode.LowLatency;
                    _logger.LogDebug("GC latency mode set to LowLatency due to high memory usage");
                }
            }
            else if (memoryUsagePercent < 0.5)
            {
                if (GCSettings.LatencyMode != GCLatencyMode.Interactive)
                {
                    GCSettings.LatencyMode = GCLatencyMode.Interactive;
                    _logger.LogTrace("GC latency mode set to Interactive");
                }
            }

            await Task.CompletedTask;
        }

        public bool CanStartNewProcessingTask()
        {
            lock (_lockObject)
            {
                var memoryUsagePercent = (double)_currentMemoryUsage / _maxMemoryUsageBytes;
                return memoryUsagePercent < _memoryCriticalThreshold;
            }
        }

        public void RegisterProcessingTaskStart()
        {
            lock (_lockObject)
            {
                _activeProcessingTasks++;
                _logger.LogTrace("Processing task started. Active tasks: {ActiveTasks}", _activeProcessingTasks);
            }
        }

        public void RegisterProcessingTaskEnd()
        {
            lock (_lockObject)
            {
                _activeProcessingTasks = Math.Max(0, _activeProcessingTasks - 1);
                _logger.LogTrace("Processing task ended. Active tasks: {ActiveTasks}", _activeProcessingTasks);
            }
        }

        public MemoryStatus GetMemoryStatus()
        {
            lock (_lockObject)
            {
                var process = Process.GetCurrentProcess();
                var gcTotalMemory = GC.GetTotalMemory(false);
                
                return new MemoryStatus
                {
                    CurrentMemoryUsageBytes = _currentMemoryUsage,
                    PeakMemoryUsageBytes = _peakMemoryUsage,
                    MaxMemoryUsageBytes = _maxMemoryUsageBytes,
                    MemoryUsagePercent = (double)_currentMemoryUsage / _maxMemoryUsageBytes,
                    GCTotalMemoryBytes = gcTotalMemory,
                    ActiveProcessingTasks = _activeProcessingTasks,
                    WorkingSetBytes = process.WorkingSet64,
                    PrivateMemoryBytes = process.PrivateMemorySize64,
                    CanStartNewTask = CanStartNewProcessingTask()
                };
            }
        }

        public async Task<bool> RequestMemoryForTaskAsync(long estimatedMemoryBytes)
        {
            lock (_lockObject)
            {
                var projectedUsage = _currentMemoryUsage + estimatedMemoryBytes;
                var projectedPercent = (double)projectedUsage / _maxMemoryUsageBytes;

                if (projectedPercent > _memoryCriticalThreshold)
                {
                    _logger.LogWarning("Memory request denied: projected usage {ProjectedMB:F2} MB ({ProjectedPercent:P1}) exceeds critical threshold",
                        projectedUsage / 1024.0 / 1024.0, projectedPercent);
                    return false;
                }

                _logger.LogTrace("Memory request approved: {RequestedMB:F2} MB (projected: {ProjectedMB:F2} MB, {ProjectedPercent:P1})",
                    estimatedMemoryBytes / 1024.0 / 1024.0,
                    projectedUsage / 1024.0 / 1024.0,
                    projectedPercent);
                return true;
            }
        }

        public async Task ForceGarbageCollectionAsync()
        {
            _logger.LogInformation("Force garbage collection requested");
            
            var beforeMemory = GC.GetTotalMemory(false);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var afterMemory = GC.GetTotalMemory(false);
            var freedMemory = beforeMemory - afterMemory;
            
            _logger.LogInformation("Force garbage collection completed: {FreedMB:F2} MB freed (before: {BeforeMB:F2} MB, after: {AfterMB:F2} MB)",
                freedMemory / 1024.0 / 1024.0,
                beforeMemory / 1024.0 / 1024.0,
                afterMemory / 1024.0 / 1024.0);

            await Task.CompletedTask;
        }
    }

    public class MemoryStatus
    {
        public long CurrentMemoryUsageBytes { get; set; }
        public long PeakMemoryUsageBytes { get; set; }
        public long MaxMemoryUsageBytes { get; set; }
        public double MemoryUsagePercent { get; set; }
        public long GCTotalMemoryBytes { get; set; }
        public int ActiveProcessingTasks { get; set; }
        public long WorkingSetBytes { get; set; }
        public long PrivateMemoryBytes { get; set; }
        public bool CanStartNewTask { get; set; }

        public double CurrentMemoryUsageMB => CurrentMemoryUsageBytes / 1024.0 / 1024.0;
        public double PeakMemoryUsageMB => PeakMemoryUsageBytes / 1024.0 / 1024.0;
        public double MaxMemoryUsageMB => MaxMemoryUsageBytes / 1024.0 / 1024.0;
        public double GCTotalMemoryMB => GCTotalMemoryBytes / 1024.0 / 1024.0;
        public double WorkingSetMB => WorkingSetBytes / 1024.0 / 1024.0;
        public double PrivateMemoryMB => PrivateMemoryBytes / 1024.0 / 1024.0;
    }
}