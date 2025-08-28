using System.ComponentModel.DataAnnotations;

namespace ThreadedMosaic.Core.Data.Models
{
    /// <summary>
    /// Database model for image metadata and caching
    /// </summary>
    public class ImageMetadata
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string FileName { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [MaxLength(64)]
        public string? FileHash { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }

        [MaxLength(50)]
        public string? Format { get; set; }

        public byte AverageRed { get; set; }
        public byte AverageGreen { get; set; }
        public byte AverageBlue { get; set; }

        public double AverageHue { get; set; }
        public double AverageSaturation { get; set; }
        public double AverageBrightness { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }

        public bool IsValid { get; set; } = true;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public virtual ICollection<MosaicProcessingResult> MosaicsAsMaster { get; set; } = new List<MosaicProcessingResult>();
        public virtual ICollection<MosaicSeedImage> MosaicsAsSeed { get; set; } = new List<MosaicSeedImage>();
    }
}