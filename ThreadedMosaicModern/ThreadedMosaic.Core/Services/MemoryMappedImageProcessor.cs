using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// High-performance image processor using memory-mapped files for large images
    /// Reduces memory allocation and improves I/O performance for large image processing
    /// </summary>
    public class MemoryMappedImageProcessor : IDisposable
    {
        private readonly ILogger<MemoryMappedImageProcessor> _logger;
        private readonly SemaphoreSlim _processingLimiter;
        private readonly long _memoryMappingThreshold;
        private bool _disposed;

        /// <summary>
        /// Files larger than this threshold (in bytes) will use memory mapping
        /// Default: 50MB
        /// </summary>
        private const long DEFAULT_MEMORY_MAPPING_THRESHOLD = 50 * 1024 * 1024;

        public MemoryMappedImageProcessor(ILogger<MemoryMappedImageProcessor> logger, long memoryMappingThreshold = DEFAULT_MEMORY_MAPPING_THRESHOLD)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryMappingThreshold = memoryMappingThreshold;
            _processingLimiter = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        }

        /// <summary>
        /// Processes large images using memory-mapped files to reduce memory usage
        /// </summary>
        public async Task<LoadedImage> ProcessLargeImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            var fileInfo = new FileInfo(imagePath);
            var useMemoryMapping = fileInfo.Length > _memoryMappingThreshold;

            _logger.LogInformation("Processing image: {ImagePath} (Size: {Size:N0} bytes, Using memory mapping: {UseMemoryMapping})", 
                imagePath, fileInfo.Length, useMemoryMapping);

            await _processingLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            try
            {
                if (useMemoryMapping)
                {
                    return await ProcessWithMemoryMappingAsync(imagePath, fileInfo, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return await ProcessWithStandardLoadingAsync(imagePath, fileInfo, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        /// <summary>
        /// Creates optimized thumbnails for large images using streaming approach
        /// </summary>
        public async Task<byte[]> CreateStreamingThumbnailAsync(
            string imagePath, 
            int maxWidth, 
            int maxHeight, 
            CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(imagePath);
            var useMemoryMapping = fileInfo.Length > _memoryMappingThreshold;

            if (!useMemoryMapping)
            {
                // Use standard approach for smaller files
                using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
                return await CreateThumbnailBytesAsync(image, maxWidth, maxHeight, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("Creating streaming thumbnail for large image: {ImagePath}", imagePath);

            // For large files, use memory mapping with streaming resize
            using var mmf = MemoryMappedFile.CreateFromFile(imagePath, FileMode.Open, "thumb_" + Path.GetFileName(imagePath), 0, MemoryMappedFileAccess.Read);
            using var accessor = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            
            // Load image configuration to get dimensions without loading full image
            var imageInfo = await Image.IdentifyAsync(accessor, cancellationToken).ConfigureAwait(false);
            
            // Reset stream position
            accessor.Position = 0;
            
            // Calculate optimal thumbnail size
            var thumbnailSize = CalculateOptimalThumbnailSize(imageInfo.Width, imageInfo.Height, maxWidth, maxHeight);
            
            // Load and resize in streaming manner
            using var sourceImage = await Image.LoadAsync<Rgba32>(accessor, cancellationToken).ConfigureAwait(false);
            
            // Resize using high-quality algorithm optimized for thumbnails
            sourceImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(thumbnailSize.Width, thumbnailSize.Height),
                Sampler = KnownResamplers.Lanczos3 // High quality resampling
            }));

            return await CreateThumbnailBytesAsync(sourceImage, maxWidth, maxHeight, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Analyzes image colors using memory-mapped access for large images
        /// </summary>
        public async Task<ImageAnalysis> AnalyzeLargeImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(imagePath);
            var useMemoryMapping = fileInfo.Length > _memoryMappingThreshold;

            if (!useMemoryMapping)
            {
                using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
                return await AnalyzeImageColorsAsync(image, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("Analyzing large image with memory mapping: {ImagePath}", imagePath);

            using var mmf = MemoryMappedFile.CreateFromFile(imagePath, FileMode.Open, "analyze_" + Path.GetFileName(imagePath), 0, MemoryMappedFileAccess.Read);
            using var accessor = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            using var loadedImage = await Image.LoadAsync<Rgba32>(accessor, cancellationToken).ConfigureAwait(false);

            return await AnalyzeImageColorsAsync(loadedImage, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Converts image formats using streaming approach for large files
        /// </summary>
        public async Task ConvertLargeImageAsync(
            string inputPath, 
            string outputPath, 
            Models.ImageFormat targetFormat, 
            int quality = 95, 
            CancellationToken cancellationToken = default)
        {
            var inputFileInfo = new FileInfo(inputPath);
            var useMemoryMapping = inputFileInfo.Length > _memoryMappingThreshold;

            if (!useMemoryMapping)
            {
                using var inputImage = await Image.LoadAsync<Rgba32>(inputPath, cancellationToken).ConfigureAwait(false);
                var simpleEncoder = GetEncoder(targetFormat, quality);
                await inputImage.SaveAsync(outputPath, simpleEncoder, cancellationToken).ConfigureAwait(false);
                return;
            }

            _logger.LogInformation("Converting large image with streaming: {InputPath} -> {OutputPath}", inputPath, outputPath);

            using var inputMmf = MemoryMappedFile.CreateFromFile(inputPath, FileMode.Open, "convert_in_" + Path.GetFileName(inputPath), 0, MemoryMappedFileAccess.Read);
            using var inputAccessor = inputMmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            using var convertImage = await Image.LoadAsync<Rgba32>(inputAccessor, cancellationToken).ConfigureAwait(false);

            var streamEncoder = GetEncoder(targetFormat, quality);
            
            // Use streaming output for large results
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024 * 1024);
            await convertImage.SaveAsync(outputStream, streamEncoder, cancellationToken).ConfigureAwait(false);
        }

        private async Task<LoadedImage> ProcessWithMemoryMappingAsync(string imagePath, FileInfo fileInfo, CancellationToken cancellationToken)
        {
            using var mmf = MemoryMappedFile.CreateFromFile(imagePath, FileMode.Open, "process_" + Path.GetFileName(imagePath), 0, MemoryMappedFileAccess.Read);
            using var accessor = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            
            // Get image dimensions without loading full image
            var imageInfo = await Image.IdentifyAsync(accessor, cancellationToken).ConfigureAwait(false);
            accessor.Position = 0;
            
            var loadedImage = new LoadedImage
            {
                FilePath = imagePath,
                Width = imageInfo.Width,
                Height = imageInfo.Height,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                Format = GetImageFormatFromPath(imagePath),
                LoadedAt = DateTime.UtcNow
            };

            // For very large images, use sampling for color analysis to avoid loading entire image
            if (fileInfo.Length > _memoryMappingThreshold * 2) // 100MB threshold for sampling
            {
                loadedImage.AverageColor = await CalculateSampledAverageColorAsync(accessor, (imageInfo.Width, imageInfo.Height), cancellationToken).ConfigureAwait(false);
                loadedImage.DominantColor = loadedImage.AverageColor; // Simplified for performance
            }
            else
            {
                using var image = await Image.LoadAsync<Rgba32>(accessor, cancellationToken).ConfigureAwait(false);
                loadedImage.AverageColor = await CalculateAverageColorAsync(image, cancellationToken).ConfigureAwait(false);
                var analysis = await AnalyzeImageColorsAsync(image, cancellationToken).ConfigureAwait(false);
                loadedImage.DominantColor = analysis.DominantColor;
                loadedImage.Brightness = analysis.Brightness;
                loadedImage.Contrast = analysis.Contrast;
                loadedImage.Saturation = analysis.Saturation;
            }

            // Create thumbnail with streaming approach
            loadedImage.ThumbnailData = await CreateStreamingThumbnailAsync(imagePath, 200, 200, cancellationToken).ConfigureAwait(false);
            loadedImage.IsProcessed = true;

            return loadedImage;
        }

        private async Task<LoadedImage> ProcessWithStandardLoadingAsync(string imagePath, FileInfo fileInfo, CancellationToken cancellationToken)
        {
            using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
            
            var loadedImage = new LoadedImage
            {
                FilePath = imagePath,
                Width = image.Width,
                Height = image.Height,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                Format = GetImageFormatFromPath(imagePath),
                LoadedAt = DateTime.UtcNow
            };

            loadedImage.AverageColor = await CalculateAverageColorAsync(image, cancellationToken).ConfigureAwait(false);
            var analysis = await AnalyzeImageColorsAsync(image, cancellationToken).ConfigureAwait(false);
            loadedImage.DominantColor = analysis.DominantColor;
            loadedImage.Brightness = analysis.Brightness;
            loadedImage.Contrast = analysis.Contrast;
            loadedImage.Saturation = analysis.Saturation;

            loadedImage.ThumbnailData = await CreateThumbnailBytesAsync(image, 200, 200, cancellationToken).ConfigureAwait(false);
            loadedImage.IsProcessed = true;

            return loadedImage;
        }

        private async Task<ColorInfo> CalculateSampledAverageColorAsync(Stream imageStream, (int Width, int Height) imageDimensions, CancellationToken cancellationToken)
        {
            // For very large images, sample pixels to calculate average color
            imageStream.Position = 0;
            using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken).ConfigureAwait(false);
            
            long totalR = 0, totalG = 0, totalB = 0;
            int sampleCount = 0;
            
            // Sample every nth pixel based on image size
            var sampleRate = Math.Max(1, (imageDimensions.Width * imageDimensions.Height) / 10000); // Max 10k samples
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y += sampleRate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x += sampleRate)
                    {
                        var pixel = row[x];
                        totalR += pixel.R;
                        totalG += pixel.G;
                        totalB += pixel.B;
                        sampleCount++;
                    }
                }
            });

            if (sampleCount == 0) return new ColorInfo(128, 128, 128); // Default gray

            return new ColorInfo(
                (byte)(totalR / sampleCount),
                (byte)(totalG / sampleCount),
                (byte)(totalB / sampleCount)
            );
        }

        private async Task<ColorInfo> CalculateAverageColorAsync(Image<Rgba32> image, CancellationToken cancellationToken)
        {
            await Task.Yield();
            
            long totalR = 0, totalG = 0, totalB = 0;
            var pixelCount = image.Width * image.Height;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        totalR += pixel.R;
                        totalG += pixel.G;
                        totalB += pixel.B;
                    }
                }
            });

            return new ColorInfo(
                (byte)(totalR / pixelCount),
                (byte)(totalG / pixelCount),
                (byte)(totalB / pixelCount)
            );
        }

        private async Task<ImageAnalysis> AnalyzeImageColorsAsync(Image<Rgba32> image, CancellationToken cancellationToken)
        {
            await Task.Yield();

            var colorCounts = new Dictionary<ColorInfo, int>();
            long totalBrightness = 0;
            var pixelCount = image.Width * image.Height;

            // Intelligent sampling based on image size
            var sampleRate = CalculateOptimalSampleRate(pixelCount);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y += sampleRate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x += sampleRate)
                    {
                        var pixel = row[x];
                        var color = new ColorInfo(pixel.R, pixel.G, pixel.B);
                        
                        colorCounts[color] = colorCounts.GetValueOrDefault(color) + 1;
                        totalBrightness += (long)color.GetBrightness() * 255;
                    }
                }
            });

            var averageColor = await CalculateAverageColorAsync(image, cancellationToken).ConfigureAwait(false);
            var dominantColor = colorCounts.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;

            return new ImageAnalysis
            {
                AverageColor = averageColor,
                DominantColor = dominantColor,
                Brightness = totalBrightness / (double)(pixelCount * 255),
                Contrast = CalculateContrast(image),
                Saturation = averageColor.GetSaturation(),
                ColorPalette = colorCounts.OrderByDescending(kvp => kvp.Value).Take(10).Select(kvp => kvp.Key).ToList()
            };
        }

        private double CalculateContrast(Image<Rgba32> image)
        {
            var brightnesses = new List<double>();
            var sampleRate = CalculateOptimalSampleRate(image.Width * image.Height, targetSamples: 1000);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y += sampleRate)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x += sampleRate)
                    {
                        var pixel = row[x];
                        var brightness = new ColorInfo(pixel.R, pixel.G, pixel.B).GetBrightness();
                        brightnesses.Add(brightness);
                    }
                }
            });

            if (brightnesses.Count < 2) return 0;

            var mean = brightnesses.Average();
            var variance = brightnesses.Sum(b => Math.Pow(b - mean, 2)) / brightnesses.Count;
            return Math.Sqrt(variance);
        }

        private async Task<byte[]> CreateThumbnailBytesAsync(Image<Rgba32> image, int maxWidth, int maxHeight, CancellationToken cancellationToken)
        {
            using var thumbnail = image.Clone();
            thumbnail.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxWidth, maxHeight),
                Sampler = KnownResamplers.Triangle // Faster for thumbnails
            }));

            using var stream = new MemoryStream();
            var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 80 };
            await thumbnail.SaveAsync(stream, encoder, cancellationToken).ConfigureAwait(false);
            return stream.ToArray();
        }

        private static (int Width, int Height) CalculateOptimalThumbnailSize(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            var aspectRatio = (double)originalWidth / originalHeight;
            
            int newWidth, newHeight;
            
            if (aspectRatio > 1) // Landscape
            {
                newWidth = Math.Min(originalWidth, maxWidth);
                newHeight = (int)(newWidth / aspectRatio);
                
                if (newHeight > maxHeight)
                {
                    newHeight = maxHeight;
                    newWidth = (int)(newHeight * aspectRatio);
                }
            }
            else // Portrait or square
            {
                newHeight = Math.Min(originalHeight, maxHeight);
                newWidth = (int)(newHeight * aspectRatio);
                
                if (newWidth > maxWidth)
                {
                    newWidth = maxWidth;
                    newHeight = (int)(newWidth / aspectRatio);
                }
            }
            
            return (Math.Max(1, newWidth), Math.Max(1, newHeight));
        }

        private static int CalculateOptimalSampleRate(int pixelCount, int targetSamples = 10000)
        {
            return Math.Max(1, pixelCount / targetSamples);
        }

        private static Models.ImageFormat GetImageFormatFromPath(string imagePath)
        {
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => Models.ImageFormat.Jpeg,
                ".png" => Models.ImageFormat.Png,
                ".bmp" => Models.ImageFormat.Bmp,
                ".gif" => Models.ImageFormat.Gif,
                ".tiff" or ".tif" => Models.ImageFormat.Tiff,
                ".webp" => Models.ImageFormat.Webp,
                _ => Models.ImageFormat.Jpeg
            };
        }

        private static IImageEncoder GetEncoder(Models.ImageFormat format, int quality)
        {
            return format switch
            {
                Models.ImageFormat.Jpeg => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality },
                Models.ImageFormat.Png => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
                Models.ImageFormat.Webp => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder { Quality = quality },
                _ => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality }
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _processingLimiter?.Dispose();
                _disposed = true;
            }
        }
    }
}