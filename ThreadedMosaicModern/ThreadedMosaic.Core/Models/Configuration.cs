using System;
using System.Collections.Generic;
using System.IO;

namespace ThreadedMosaic.Core.Models
{
    /// <summary>
    /// Application configuration settings
    /// </summary>
    public class MosaicConfiguration
    {
        /// <summary>
        /// Image processing settings
        /// </summary>
        public ImageProcessingSettings ImageProcessing { get; set; } = new();
        
        /// <summary>
        /// Performance settings
        /// </summary>
        public PerformanceSettings Performance { get; set; } = new();
        
        /// <summary>
        /// File management settings
        /// </summary>
        public FileManagementSettings FileManagement { get; set; } = new();
        
        /// <summary>
        /// Logging settings
        /// </summary>
        public LoggingSettings Logging { get; set; } = new();
        
        /// <summary>
        /// Quality settings
        /// </summary>
        public QualitySettings Quality { get; set; } = new();
    }

    /// <summary>
    /// Image processing configuration
    /// </summary>
    public class ImageProcessingSettings
    {
        /// <summary>
        /// Maximum image dimensions for processing
        /// </summary>
        public int MaxImageWidth { get; set; } = 4096;
        public int MaxImageHeight { get; set; } = 4096;
        
        /// <summary>
        /// Thumbnail settings
        /// </summary>
        public int DefaultThumbnailWidth { get; set; } = 200;
        public int DefaultThumbnailHeight { get; set; } = 200;
        
        /// <summary>
        /// Supported image formats
        /// </summary>
        public List<string> SupportedFormats { get; set; } = new() { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
        
        /// <summary>
        /// Color matching settings
        /// </summary>
        public double DefaultColorTolerance { get; set; } = 10.0;
        public double DefaultHueTolerance { get; set; } = 15.0;
        
        /// <summary>
        /// Whether to use GPU acceleration if available
        /// </summary>
        public bool UseGpuAcceleration { get; set; } = false;
        
        /// <summary>
        /// Image cache settings
        /// </summary>
        public bool EnableImageCache { get; set; } = true;
        public int ImageCacheMaxSize { get; set; } = 100; // number of images
        public TimeSpan ImageCacheExpiration { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Performance and concurrency settings
    /// </summary>
    public class PerformanceSettings
    {
        /// <summary>
        /// Maximum number of concurrent processing tasks
        /// </summary>
        public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount;
        
        /// <summary>
        /// Maximum memory usage in MB
        /// </summary>
        public long MaxMemoryUsageMB { get; set; } = 1024;
        
        /// <summary>
        /// Parallel processing settings
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
        
        /// <summary>
        /// Background processing settings
        /// </summary>
        public bool EnableBackgroundProcessing { get; set; } = true;
        public int BackgroundTaskQueueSize { get; set; } = 10;
        
        /// <summary>
        /// Resource monitoring settings
        /// </summary>
        public bool EnableResourceMonitoring { get; set; } = true;
        public TimeSpan ResourceMonitoringInterval { get; set; } = TimeSpan.FromSeconds(5);
        
        /// <summary>
        /// Progress reporting settings
        /// </summary>
        public TimeSpan ProgressReportingInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// File management settings
    /// </summary>
    public class FileManagementSettings
    {
        /// <summary>
        /// Temporary file settings
        /// </summary>
        public string TempDirectory { get; set; } = Path.GetTempPath();
        public TimeSpan TempFileCleanupAge { get; set; } = TimeSpan.FromDays(1);
        public bool AutoCleanupTempFiles { get; set; } = true;
        
        /// <summary>
        /// File size limits
        /// </summary>
        public long MaxUploadFileSizeMB { get; set; } = 50;
        public long MaxOutputFileSizeMB { get; set; } = 500;
        
        /// <summary>
        /// Disk space requirements
        /// </summary>
        public long MinimumFreeDiskSpaceMB { get; set; } = 1024;
        
        /// <summary>
        /// File naming settings
        /// </summary>
        public string OutputFileNamePattern { get; set; } = "mosaic_{timestamp}_{type}";
        public bool OverwriteExistingFiles { get; set; } = false;
        
        /// <summary>
        /// Backup settings
        /// </summary>
        public bool CreateBackupCopies { get; set; } = false;
        public int MaxBackupCopies { get; set; } = 3;
    }

    /// <summary>
    /// Logging configuration
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Log level settings
        /// </summary>
        public LogLevel DefaultLogLevel { get; set; } = LogLevel.Information;
        public Dictionary<string, LogLevel> CategoryLogLevels { get; set; } = new();
        
        /// <summary>
        /// File logging settings
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;
        public string LogDirectory { get; set; } = "logs";
        public string LogFileNamePattern { get; set; } = "mosaic_{date}.log";
        public long MaxLogFileSizeMB { get; set; } = 10;
        public int MaxLogFiles { get; set; } = 30;
        
        /// <summary>
        /// Console logging settings
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;
        public bool UseStructuredLogging { get; set; } = true;
        
        /// <summary>
        /// Performance logging
        /// </summary>
        public bool LogPerformanceMetrics { get; set; } = true;
        public bool LogMemoryUsage { get; set; } = true;
        public bool LogDetailedTiming { get; set; } = false;
    }

    /// <summary>
    /// Quality and optimization settings
    /// </summary>
    public class QualitySettings
    {
        /// <summary>
        /// Default output quality settings
        /// </summary>
        public Dictionary<ImageFormat, int> DefaultQuality { get; set; } = new()
        {
            { ImageFormat.Jpeg, 85 },
            { ImageFormat.Webp, 80 },
            { ImageFormat.Png, 100 },
            { ImageFormat.Bmp, 100 }
        };
        
        /// <summary>
        /// Optimization settings
        /// </summary>
        public bool OptimizeForFileSize { get; set; } = false;
        public bool OptimizeForQuality { get; set; } = true;
        public bool UseProgressiveJpeg { get; set; } = true;
        
        /// <summary>
        /// Color processing settings
        /// </summary>
        public bool UseColorProfiling { get; set; } = false;
        public bool PreserveMetadata { get; set; } = true;
        
        /// <summary>
        /// Validation settings
        /// </summary>
        public bool StrictValidation { get; set; } = true;
        public bool ValidateImageIntegrity { get; set; } = true;
    }

    /// <summary>
    /// Log levels enumeration
    /// </summary>
    public enum LogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }
}