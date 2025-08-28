using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Background service for cleaning up temporary files and monitoring disk space
    /// </summary>
    public class TempFileCleanupService : BackgroundService
    {
        private readonly ILogger<TempFileCleanupService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _cleanupInterval;
        private readonly int _tempFileCleanupAgeDays;
        private readonly long _minimumFreeDiskSpaceMB;
        private readonly string _tempDirectory;

        public TempFileCleanupService(ILogger<TempFileCleanupService> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Load configuration settings
            _cleanupInterval = TimeSpan.FromHours(_configuration.GetValue<double>("MosaicConfiguration:FileManagement:CleanupIntervalHours", 6));
            _tempFileCleanupAgeDays = _configuration.GetValue<int>("MosaicConfiguration:FileManagement:TempFileCleanupAgeDays", 1);
            _minimumFreeDiskSpaceMB = _configuration.GetValue<long>("MosaicConfiguration:FileManagement:MinimumFreeDiskSpaceMB", 1024);
            _tempDirectory = _configuration.GetValue<string>("MosaicConfiguration:FileManagement:TempDirectory", "") 
                ?? Path.Combine(Path.GetTempPath(), "ThreadedMosaic");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TempFileCleanupService started. Cleanup interval: {Interval}, Age threshold: {Days} days", 
                _cleanupInterval, _tempFileCleanupAgeDays);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync(stoppingToken);
                    await CheckDiskSpaceAsync();
                    
                    // Wait for next cleanup cycle
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("TempFileCleanupService stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during temp file cleanup cycle");
                    // Continue running despite errors, but wait a bit before retrying
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task PerformCleanupAsync(CancellationToken cancellationToken)
        {
            if (!Directory.Exists(_tempDirectory))
            {
                _logger.LogDebug("Temp directory does not exist: {Directory}", _tempDirectory);
                return;
            }

            var cutoffTime = DateTime.Now.AddDays(-_tempFileCleanupAgeDays);
            var deletedFiles = 0;
            var deletedDirectories = 0;
            var reclaimedBytes = 0L;

            _logger.LogDebug("Starting cleanup of files older than {CutoffTime} in {Directory}", cutoffTime, _tempDirectory);

            try
            {
                // Clean up old files
                var files = Directory.GetFiles(_tempDirectory, "*", SearchOption.AllDirectories);
                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.LastWriteTime < cutoffTime)
                        {
                            var fileSize = fileInfo.Length;
                            File.Delete(filePath);
                            deletedFiles++;
                            reclaimedBytes += fileSize;
                            
                            _logger.LogTrace("Deleted old file: {FilePath} ({Size} bytes)", filePath, fileSize);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file: {FilePath}", filePath);
                    }
                }

                // Clean up empty directories
                var directories = Directory.GetDirectories(_tempDirectory, "*", SearchOption.AllDirectories)
                    .OrderByDescending(dir => dir.Length); // Process deepest directories first

                foreach (var dirPath in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var dirInfo = new DirectoryInfo(dirPath);
                        if (dirInfo.LastWriteTime < cutoffTime && !dirInfo.GetFileSystemInfos().Any())
                        {
                            Directory.Delete(dirPath);
                            deletedDirectories++;
                            _logger.LogTrace("Deleted empty directory: {DirectoryPath}", dirPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete directory: {DirectoryPath}", dirPath);
                    }
                }

                if (deletedFiles > 0 || deletedDirectories > 0)
                {
                    _logger.LogInformation("Cleanup completed: {DeletedFiles} files and {DeletedDirectories} directories deleted, {ReclaimedMB:F2} MB reclaimed",
                        deletedFiles, deletedDirectories, reclaimedBytes / 1024.0 / 1024.0);
                }
                else
                {
                    _logger.LogDebug("Cleanup completed: no old files found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file cleanup in directory: {Directory}", _tempDirectory);
            }

            await Task.CompletedTask;
        }

        private async Task CheckDiskSpaceAsync()
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(_tempDirectory) ?? "C:");
                var availableSpaceMB = driveInfo.AvailableFreeSpace / 1024 / 1024;

                _logger.LogTrace("Available disk space: {AvailableSpaceMB} MB (minimum required: {MinimumSpaceMB} MB)", 
                    availableSpaceMB, _minimumFreeDiskSpaceMB);

                if (availableSpaceMB < _minimumFreeDiskSpaceMB)
                {
                    _logger.LogWarning("Low disk space detected: {AvailableSpaceMB} MB available, {MinimumSpaceMB} MB required", 
                        availableSpaceMB, _minimumFreeDiskSpaceMB);
                        
                    // Perform emergency cleanup - reduce age threshold
                    await PerformEmergencyCleanupAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking disk space");
            }

            await Task.CompletedTask;
        }

        private async Task PerformEmergencyCleanupAsync()
        {
            if (!Directory.Exists(_tempDirectory))
                return;

            _logger.LogInformation("Performing emergency cleanup due to low disk space");

            try
            {
                // More aggressive cleanup - reduce age threshold to 4 hours
                var emergencyCutoffTime = DateTime.Now.AddHours(-4);
                var deletedFiles = 0;
                var reclaimedBytes = 0L;

                var files = Directory.GetFiles(_tempDirectory, "*", SearchOption.AllDirectories);
                foreach (var filePath in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.LastWriteTime < emergencyCutoffTime)
                        {
                            var fileSize = fileInfo.Length;
                            File.Delete(filePath);
                            deletedFiles++;
                            reclaimedBytes += fileSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file during emergency cleanup: {FilePath}", filePath);
                    }
                }

                _logger.LogWarning("Emergency cleanup completed: {DeletedFiles} files deleted, {ReclaimedMB:F2} MB reclaimed",
                    deletedFiles, reclaimedBytes / 1024.0 / 1024.0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during emergency cleanup");
            }

            await Task.CompletedTask;
        }

        public Task<CleanupResult> ForceCleanupAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Force cleanup requested");
            
            var deletedFiles = 0;
            var deletedDirectories = 0;
            var reclaimedBytes = 0L;

            if (!Directory.Exists(_tempDirectory))
            {
                return Task.FromResult(new CleanupResult(0, 0, 0));
            }

            try
            {
                var cutoffTime = DateTime.Now.AddDays(-_tempFileCleanupAgeDays);

                // Clean up files
                var files = Directory.GetFiles(_tempDirectory, "*", SearchOption.AllDirectories);
                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.LastWriteTime < cutoffTime)
                        {
                            var fileSize = fileInfo.Length;
                            File.Delete(filePath);
                            deletedFiles++;
                            reclaimedBytes += fileSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file during force cleanup: {FilePath}", filePath);
                    }
                }

                // Clean up directories
                var directories = Directory.GetDirectories(_tempDirectory, "*", SearchOption.AllDirectories)
                    .OrderByDescending(dir => dir.Length);

                foreach (var dirPath in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var dirInfo = new DirectoryInfo(dirPath);
                        if (!dirInfo.GetFileSystemInfos().Any())
                        {
                            Directory.Delete(dirPath);
                            deletedDirectories++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete directory during force cleanup: {DirectoryPath}", dirPath);
                    }
                }

                _logger.LogInformation("Force cleanup completed: {DeletedFiles} files and {DeletedDirectories} directories deleted, {ReclaimedMB:F2} MB reclaimed",
                    deletedFiles, deletedDirectories, reclaimedBytes / 1024.0 / 1024.0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during force cleanup");
            }

            return Task.FromResult(new CleanupResult(deletedFiles, deletedDirectories, reclaimedBytes));
        }

        public Task<long> GetAvailableDiskSpaceAsync()
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(_tempDirectory) ?? "C:");
                return Task.FromResult(driveInfo.AvailableFreeSpace);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available disk space");
                return Task.FromResult(-1L);
            }
        }
    }

    public record CleanupResult(int DeletedFiles, int DeletedDirectories, long ReclaimedBytes)
    {
        public double ReclaimedMB => ReclaimedBytes / 1024.0 / 1024.0;
    }
}