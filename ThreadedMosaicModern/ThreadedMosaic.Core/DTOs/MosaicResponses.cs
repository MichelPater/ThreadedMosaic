using System;
using System.Collections.Generic;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.DTOs
{
    /// <summary>
    /// Result of a mosaic creation operation
    /// </summary>
    public class MosaicResult
    {
        public Guid MosaicId { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public MosaicStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? ProcessingTime { get; set; }
        public long OutputFileSize { get; set; }
        public int TotalTiles { get; set; }
        public int ProcessedTiles { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public MosaicStatistics? Statistics { get; set; }
    }

    /// <summary>
    /// Current status of a mosaic operation
    /// </summary>
    public enum MosaicStatus
    {
        Unknown,
        Pending,
        Initializing,
        LoadingImages,
        AnalyzingImages,
        CreatingMosaic,
        Saving,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Detailed status information for a mosaic operation
    /// </summary>
    public class MosaicStatusDetail
    {
        public Guid MosaicId { get; set; }
        public MosaicStatus Status { get; set; }
        public double ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public int CurrentStepNumber { get; set; }
        public int TotalSteps { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public string? StatusMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics about the mosaic creation process
    /// </summary>
    public class MosaicStatistics
    {
        public int TotalSeedImages { get; set; }
        public int ValidSeedImages { get; set; }
        public int UniqueTilesUsed { get; set; }
        public int MostUsedTileCount { get; set; }
        public string? MostUsedTilePath { get; set; }
        public double AverageColorDistance { get; set; }
        public double ProcessingSpeed { get; set; } // tiles per second
        public long MemoryUsed { get; set; } // bytes
        public long PeakMemoryUsage { get; set; } // bytes
        public Dictionary<string, int> ImageUsageCount { get; set; } = new();
        public List<ColorInfo> DominantColors { get; set; } = new();
    }

    /// <summary>
    /// Result of validating a mosaic request
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
        
        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Failure(params string[] errors) => new() 
        { 
            IsValid = false, 
            Errors = new List<string>(errors) 
        };
    }

    /// <summary>
    /// Response for file upload operations
    /// </summary>
    public class FileUploadResponse
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public ImageFormat? Format { get; set; }
    }

    /// <summary>
    /// Result of image analysis
    /// </summary>
    public class ImageAnalysis
    {
        public ColorInfo AverageColor { get; set; }
        public ColorInfo DominantColor { get; set; }
        public List<ColorInfo> ColorPalette { get; set; } = new();
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public double Saturation { get; set; }
        public double Sharpness { get; set; }
        public bool HasTransparency { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result of file upload operation for multiple files
    /// </summary>
    public class FileUploadResult
    {
        public string Message { get; set; } = string.Empty;
        public string UploadDirectory { get; set; } = string.Empty;
        public List<UploadedFile> Files { get; set; } = new();
    }

    /// <summary>
    /// Information about an uploaded file
    /// </summary>
    public class UploadedFile
    {
        public string OriginalName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }
}