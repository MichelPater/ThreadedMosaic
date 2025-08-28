using System;

namespace ThreadedMosaic.Core.Models
{
    /// <summary>
    /// Represents an Image loaded into memory with enhanced metadata support
    /// </summary>
    public class LoadedImage : IDisposable
    {
        public ColorInfo AverageColor { get; set; } = new ColorInfo();
        public string FilePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public ImageFormat Format { get; set; }
        public byte[]? ImageData { get; set; }
        public byte[]? ThumbnailData { get; set; }
        
        /// <summary>
        /// Additional color information for better matching
        /// </summary>
        public ColorInfo DominantColor { get; set; } = new ColorInfo();
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public double Saturation { get; set; }
        
        /// <summary>
        /// Metadata for processing
        /// </summary>
        public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
        public bool IsProcessed { get; set; }
        public string? ProcessingError { get; set; }

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                ImageData = null;
                ThumbnailData = null;
                _disposed = true;
            }
        }

        ~LoadedImage()
        {
            Dispose(false);
        }
    }
}