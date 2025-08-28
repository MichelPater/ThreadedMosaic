using System.ComponentModel.DataAnnotations;

namespace ThreadedMosaic.Core.Data.Models
{
    /// <summary>
    /// Database model for tracking detailed processing steps and performance
    /// </summary>
    public class ProcessingStep
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid MosaicResultId { get; set; }
        public virtual MosaicProcessingResult MosaicResult { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string StepName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed

        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        public int? ItemsProcessed { get; set; }
        public int? TotalItems { get; set; }
        public double? ProgressPercentage { get; set; }

        public long? MemoryUsedBytes { get; set; }
        public double? MemoryUsedMB => MemoryUsedBytes.HasValue ? MemoryUsedBytes.Value / 1024.0 / 1024.0 : null;

        [MaxLength(2000)]
        public string? AdditionalData { get; set; } // JSON for any extra metrics
    }
}