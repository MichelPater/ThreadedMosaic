using System.ComponentModel.DataAnnotations;

namespace ThreadedMosaic.Core.Data.Models
{
    /// <summary>
    /// Junction table for mosaic results and their seed images
    /// </summary>
    public class MosaicSeedImage
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid MosaicResultId { get; set; }
        public virtual MosaicProcessingResult MosaicResult { get; set; } = null!;

        public Guid SeedImageId { get; set; }
        public virtual ImageMetadata SeedImage { get; set; } = null!;

        public int UsageCount { get; set; } = 0;
        public double? AverageColorDistance { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}