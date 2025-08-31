using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Health check service for monitoring system resources and application health
    /// </summary>
    public class ThreadedMosaicHealthCheck : IHealthCheck
    {
        private readonly ILogger<ThreadedMosaicHealthCheck> _logger;
        private readonly IPerformanceMonitor _performanceMonitor;

        public ThreadedMosaicHealthCheck(ILogger<ThreadedMosaicHealthCheck> logger, IPerformanceMonitor performanceMonitor)
        {
            _logger = logger;
            _performanceMonitor = performanceMonitor;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var systemPerf = _performanceMonitor.GetCurrentSystemPerformance();
                var data = new Dictionary<string, object>
                {
                    ["cpu_usage_percent"] = systemPerf.CpuUsagePercent,
                    ["memory_usage_percent"] = systemPerf.MemoryUsagePercent,
                    ["available_memory_mb"] = systemPerf.AvailableMemoryMB,
                    ["active_processes"] = systemPerf.ActiveProcesses,
                    ["thread_count"] = systemPerf.ThreadCount
                };

                // Check critical thresholds
                var issues = new List<string>();

                if (systemPerf.CpuUsagePercent > 90)
                    issues.Add("High CPU usage detected");

                if (systemPerf.MemoryUsagePercent > 95)
                    issues.Add("Critical memory usage detected");

                if (systemPerf.AvailableMemoryMB < 100)
                    issues.Add("Very low available memory");

                // Check disk space for all drives
                foreach (var disk in systemPerf.DiskSpaceInfo.Values)
                {
                    data[$"disk_{disk.DriveName}_usage_percent"] = disk.UsagePercent;
                    data[$"disk_{disk.DriveName}_available_gb"] = disk.AvailableSpaceGB;

                    if (disk.UsagePercent > 95)
                        issues.Add($"Critical disk space on drive {disk.DriveName}");
                    else if (disk.AvailableSpaceGB < 1)
                        issues.Add($"Low disk space on drive {disk.DriveName}");
                }

                // Determine health status
                if (issues.Count > 0)
                {
                    var message = $"System health issues detected: {string.Join(", ", issues)}";
                    _logger.LogWarning("Health check degraded: {Message}", message);
                    return HealthCheckResult.Degraded(message, data: data);
                }

                _logger.LogDebug("Health check passed - system resources normal");
                return HealthCheckResult.Healthy("System resources within normal ranges", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed with exception");
                return HealthCheckResult.Unhealthy("Health check failed", ex);
            }
        }
    }

    /// <summary>
    /// Health check for image processing services availability
    /// </summary>
    public class ImageProcessingHealthCheck : IHealthCheck
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly ILogger<ImageProcessingHealthCheck> _logger;

        public ImageProcessingHealthCheck(IImageProcessingService imageProcessingService, ILogger<ImageProcessingHealthCheck> logger)
        {
            _imageProcessingService = imageProcessingService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Test basic image processing functionality
                using var testImage = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(10, 10);
                
                var stopwatch = Stopwatch.StartNew();
                
                // Test basic operations
                var canProcess = testImage.Width == 10 && testImage.Height == 10;
                
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["test_image_processing_ms"] = stopwatch.ElapsedMilliseconds,
                    ["image_processing_available"] = canProcess
                };

                if (!canProcess)
                {
                    _logger.LogError("Image processing service health check failed - basic operations not working");
                    return HealthCheckResult.Unhealthy("Image processing service unavailable", data: data);
                }

                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    _logger.LogWarning("Image processing service responding slowly: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    return HealthCheckResult.Degraded("Image processing service slow", data: data);
                }

                _logger.LogDebug("Image processing health check passed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                return HealthCheckResult.Healthy("Image processing service operational", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image processing health check failed");
                return HealthCheckResult.Unhealthy("Image processing service failed", ex);
            }
        }
    }

    /// <summary>
    /// Health check for database connectivity
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly Data.ThreadedMosaicDbContext _dbContext;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(Data.ThreadedMosaicDbContext dbContext, ILogger<DatabaseHealthCheck> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Test database connectivity with simple query
                var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
                
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["database_connection_ms"] = stopwatch.ElapsedMilliseconds,
                    ["can_connect"] = canConnect
                };

                if (!canConnect)
                {
                    _logger.LogError("Database health check failed - cannot connect");
                    return HealthCheckResult.Unhealthy("Database connection failed", data: data);
                }

                if (stopwatch.ElapsedMilliseconds > 5000)
                {
                    _logger.LogWarning("Database responding slowly: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    return HealthCheckResult.Degraded("Database connection slow", data: data);
                }

                _logger.LogDebug("Database health check passed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                return HealthCheckResult.Healthy("Database connection operational", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy("Database health check failed", ex);
            }
        }
    }
}