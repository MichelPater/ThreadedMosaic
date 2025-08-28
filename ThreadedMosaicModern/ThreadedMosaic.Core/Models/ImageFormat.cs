using System;

namespace ThreadedMosaic.Core.Models
{
    /// <summary>
    /// Supported image formats for the mosaic application
    /// </summary>
    public enum ImageFormat
    {
        Unknown,
        Jpeg,
        Png,
        Bmp,
        Gif,
        Tiff,
        Webp
    }

    /// <summary>
    /// Extension methods for ImageFormat enum
    /// </summary>
    public static class ImageFormatExtensions
    {
        /// <summary>
        /// Gets the file extension for the image format
        /// </summary>
        public static string GetFileExtension(this ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Jpeg => ".jpg",
                ImageFormat.Png => ".png",
                ImageFormat.Bmp => ".bmp",
                ImageFormat.Gif => ".gif",
                ImageFormat.Tiff => ".tiff",
                ImageFormat.Webp => ".webp",
                _ => ".jpg"
            };
        }

        /// <summary>
        /// Gets the MIME type for the image format
        /// </summary>
        public static string GetMimeType(this ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Jpeg => "image/jpeg",
                ImageFormat.Png => "image/png",
                ImageFormat.Bmp => "image/bmp",
                ImageFormat.Gif => "image/gif",
                ImageFormat.Tiff => "image/tiff",
                ImageFormat.Webp => "image/webp",
                _ => "image/jpeg"
            };
        }

        /// <summary>
        /// Determines image format from file extension
        /// </summary>
        public static ImageFormat FromFileExtension(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return ImageFormat.Unknown;

            var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".png" => ImageFormat.Png,
                ".bmp" => ImageFormat.Bmp,
                ".gif" => ImageFormat.Gif,
                ".tiff" or ".tif" => ImageFormat.Tiff,
                ".webp" => ImageFormat.Webp,
                _ => ImageFormat.Unknown
            };
        }

        /// <summary>
        /// Checks if the format supports transparency
        /// </summary>
        public static bool SupportsTransparency(this ImageFormat format)
        {
            return format is ImageFormat.Png or ImageFormat.Gif or ImageFormat.Webp;
        }

        /// <summary>
        /// Checks if the format supports quality settings
        /// </summary>
        public static bool SupportsQuality(this ImageFormat format)
        {
            return format is ImageFormat.Jpeg or ImageFormat.Webp;
        }

        /// <summary>
        /// Gets the default quality setting for formats that support it
        /// </summary>
        public static int GetDefaultQuality(this ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Jpeg => 85,
                ImageFormat.Webp => 80,
                _ => 100
            };
        }
    }
}