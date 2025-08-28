using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.DTOs
{
    /// <summary>
    /// Base class for all mosaic creation requests
    /// </summary>
    public abstract class MosaicRequestBase
    {
        [Required]
        public string MasterImagePath { get; set; } = string.Empty;

        [Required]
        public string SeedImagesDirectory { get; set; } = string.Empty;

        [Required]
        public string OutputPath { get; set; } = string.Empty;

        [Range(1, 1000)]
        public int PixelSize { get; set; } = 16;

        public ImageFormat OutputFormat { get; set; } = ImageFormat.Jpeg;

        [Range(1, 100)]
        public int Quality { get; set; } = 85;

        public bool CreateThumbnail { get; set; } = true;

        public int ThumbnailMaxWidth { get; set; } = 800;

        public int ThumbnailMaxHeight { get; set; } = 600;
    }

    /// <summary>
    /// Request for creating a color-based mosaic
    /// </summary>
    public class ColorMosaicRequest : MosaicRequestBase
    {
        /// <summary>
        /// Color matching tolerance (0-100)
        /// </summary>
        [Range(0, 100)]
        public int ColorTolerance { get; set; } = 10;

        /// <summary>
        /// Whether to apply brightness matching
        /// </summary>
        public bool UseBrightnessMatching { get; set; } = true;

        /// <summary>
        /// Transparency overlay intensity (0-100)
        /// </summary>
        [Range(0, 100)]
        public int TransparencyIntensity { get; set; } = 20;
    }

    /// <summary>
    /// Request for creating a hue-based mosaic
    /// </summary>
    public class HueMosaicRequest : MosaicRequestBase
    {
        /// <summary>
        /// Hue matching tolerance in degrees (0-360)
        /// </summary>
        [Range(0, 360)]
        public double HueTolerance { get; set; } = 15.0;

        /// <summary>
        /// Saturation weight in matching algorithm (0-100)
        /// </summary>
        [Range(0, 100)]
        public int SaturationWeight { get; set; } = 50;

        /// <summary>
        /// Brightness weight in matching algorithm (0-100)
        /// </summary>
        [Range(0, 100)]
        public int BrightnessWeight { get; set; } = 30;
    }

    /// <summary>
    /// Request for creating a photo-based mosaic
    /// </summary>
    public class PhotoMosaicRequest : MosaicRequestBase
    {
        /// <summary>
        /// Image similarity threshold (0-100)
        /// </summary>
        [Range(0, 100)]
        public int SimilarityThreshold { get; set; } = 75;

        /// <summary>
        /// Whether to avoid repeating the same image
        /// </summary>
        public bool AvoidImageRepetition { get; set; } = true;

        /// <summary>
        /// Maximum times an image can be reused
        /// </summary>
        [Range(1, 100)]
        public int MaxImageReuse { get; set; } = 3;

        /// <summary>
        /// Whether to use edge detection for better matching
        /// </summary>
        public bool UseEdgeDetection { get; set; } = false;

        /// <summary>
        /// Edge detection sensitivity (0-100)
        /// </summary>
        [Range(0, 100)]
        public int EdgeSensitivity { get; set; } = 50;
    }
}