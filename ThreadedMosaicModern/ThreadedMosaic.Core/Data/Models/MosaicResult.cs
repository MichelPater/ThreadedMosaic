using System.ComponentModel.DataAnnotations;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.Data.Models
{
    /// <summary>
    /// Database model for mosaic processing results and history
    /// </summary>
    public class MosaicProcessingResult
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string MosaicType { get; set; } = string.Empty; // Color, Hue, Photo

        public Guid MasterImageId { get; set; }
        public virtual ImageMetadata MasterImage { get; set; } = null!;

        [MaxLength(500)]
        public string? OutputPath { get; set; }

        public MosaicStatus Status { get; set; }

        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? ProcessingTime { get; set; }

        public int PixelSize { get; set; }
        public int? Quality { get; set; }

        [MaxLength(20)]
        public string? OutputFormat { get; set; }

        public int? TilesProcessed { get; set; }
        public int? TotalTiles { get; set; }
        public int? SeedImagesUsed { get; set; }
        public int? UniqueTilesUsed { get; set; }

        public double? ProcessingPercentage => TotalTiles > 0 ? (double)(TilesProcessed ?? 0) / TotalTiles * 100 : null;

        public double? AverageColorDistance { get; set; }
        public TimeSpan? AverageProcessingTimePerTile { get; set; }

        [MaxLength(500)]
        public string? MostUsedTilePath { get; set; }
        public int? MostUsedTileCount { get; set; }

        // Color mosaic specific properties
        public int? ColorTolerance { get; set; }

        // Hue mosaic specific properties
        public double? HueTolerance { get; set; }
        public int? SaturationWeight { get; set; }
        public int? BrightnessWeight { get; set; }

        // Photo mosaic specific properties
        public int? SimilarityThreshold { get; set; }
        public bool? AvoidImageRepetition { get; set; }
        public int? MaxImageReuse { get; set; }
        public bool? UseEdgeDetection { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public virtual ICollection<MosaicSeedImage> SeedImages { get; set; } = new List<MosaicSeedImage>();
        public virtual ICollection<ProcessingStep> ProcessingSteps { get; set; } = new List<ProcessingStep>();
    }
}