using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// ImageSharp-based implementation of image processing service
    /// Replaces System.Drawing with modern, cross-platform image processing
    /// </summary>
    public class ImageSharpProcessingService : IImageProcessingService, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ImageSharpProcessingService> _logger;
        private readonly Configuration _imageSharpConfig;
        private readonly SemaphoreSlim _memorySemaphore;
        private bool _disposed;

        public ImageSharpProcessingService(IMemoryCache memoryCache, ILogger<ImageSharpProcessingService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _imageSharpConfig = SixLabors.ImageSharp.Configuration.Default.Clone();
            _memorySemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        }

        public async Task<LoadedImage> LoadImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("Image path cannot be null or empty", nameof(imagePath));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            var cacheKey = $"loaded_image_{imagePath}_{File.GetLastWriteTimeUtc(imagePath).Ticks}";
            
            if (_memoryCache.TryGetValue(cacheKey, out LoadedImage? cachedImage) && cachedImage != null)
            {
                _logger.LogDebug("Loaded image from cache: {ImagePath}", imagePath);
                return cachedImage;
            }

            await _memorySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Loading image: {ImagePath}", imagePath);

                using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
                
                var loadedImage = new LoadedImage
                {
                    FilePath = imagePath,
                    Width = image.Width,
                    Height = image.Height,
                    FileSize = new FileInfo(imagePath).Length,
                    LastModified = File.GetLastWriteTimeUtc(imagePath),
                    Format = GetImageFormatFromPath(imagePath),
                    LoadedAt = DateTime.UtcNow
                };

                // Calculate average color
                loadedImage.AverageColor = await CalculateAverageColorAsync(image, cancellationToken).ConfigureAwait(false);
                
                // Calculate additional color information
                var analysis = await AnalyzeImageColorsAsync(image, cancellationToken).ConfigureAwait(false);
                loadedImage.DominantColor = analysis.DominantColor;
                loadedImage.Brightness = analysis.Brightness;
                loadedImage.Contrast = analysis.Contrast;
                loadedImage.Saturation = analysis.Saturation;

                // Create thumbnail
                loadedImage.ThumbnailData = await CreateThumbnailBytesAsync(image, 200, 200, cancellationToken).ConfigureAwait(false);

                loadedImage.IsProcessed = true;

                // Cache the loaded image
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                    Priority = CacheItemPriority.Normal
                };
                _memoryCache.Set(cacheKey, loadedImage, cacheOptions);

                _logger.LogDebug("Successfully loaded image: {ImagePath} ({Width}x{Height})", imagePath, image.Width, image.Height);
                return loadedImage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load image: {ImagePath}", imagePath);
                return new LoadedImage
                {
                    FilePath = imagePath,
                    ProcessingError = ex.Message,
                    IsProcessed = false
                };
            }
            finally
            {
                _memorySemaphore.Release();
            }
        }

        public async Task<IEnumerable<LoadedImage>> LoadImagesFromDirectoryAsync(
            string directoryPath, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp" };
            var imageFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .ToList();

            if (!imageFiles.Any())
            {
                _logger.LogWarning("No supported image files found in directory: {Directory}", directoryPath);
                return Array.Empty<LoadedImage>();
            }

            _logger.LogInformation("Found {Count} image files in directory: {Directory}", imageFiles.Count, directoryPath);

            if (progressReporter != null)
            {
                await progressReporter.SetMaximumAsync(imageFiles.Count, cancellationToken).ConfigureAwait(false);
            }

            var loadedImages = new List<LoadedImage>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);

            var loadTasks = imageFiles.Select(async (imageFile, index) =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var loadedImage = await LoadImageAsync(imageFile, cancellationToken).ConfigureAwait(false);
                    
                    if (progressReporter != null)
                    {
                        await progressReporter.ReportProgressAsync(
                            index + 1, 
                            imageFiles.Count, 
                            $"Loaded {Path.GetFileName(imageFile)}", 
                            cancellationToken).ConfigureAwait(false);
                    }

                    return loadedImage;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(loadTasks).ConfigureAwait(false);
            loadedImages.AddRange(results.Where(img => img.IsProcessed));

            _logger.LogInformation("Successfully loaded {LoadedCount}/{TotalCount} images from directory", 
                loadedImages.Count, imageFiles.Count);

            return loadedImages;
        }

        public async Task<LoadedImage> ResizeImageAsync(LoadedImage image, int width, int height, CancellationToken cancellationToken = default)
        {
            await _memorySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var sourceImage = await Image.LoadAsync<Rgba32>(image.FilePath, cancellationToken).ConfigureAwait(false);
                
                sourceImage.Mutate(x => x.Resize(width, height));

                // Create new LoadedImage with updated dimensions
                var resizedImage = new LoadedImage
                {
                    FilePath = image.FilePath,
                    Width = width,
                    Height = height,
                    FileSize = image.FileSize,
                    LastModified = image.LastModified,
                    Format = image.Format,
                    LoadedAt = DateTime.UtcNow
                };

                // Recalculate average color for resized image
                resizedImage.AverageColor = await CalculateAverageColorAsync(sourceImage, cancellationToken).ConfigureAwait(false);

                return resizedImage;
            }
            finally
            {
                _memorySemaphore.Release();
            }
        }

        public async Task<byte[]> CreateThumbnailAsync(string imagePath, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
        {
            using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
            return await CreateThumbnailBytesAsync(image, maxWidth, maxHeight, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ImageAnalysis> AnalyzeImageAsync(LoadedImage image, CancellationToken cancellationToken = default)
        {
            using var sourceImage = await Image.LoadAsync<Rgba32>(image.FilePath, cancellationToken).ConfigureAwait(false);
            return await AnalyzeImageColorsAsync(sourceImage, cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveImageAsync(LoadedImage image, string outputPath, Models.ImageFormat format, int quality = 95, CancellationToken cancellationToken = default)
        {
            using var sourceImage = await Image.LoadAsync<Rgba32>(image.FilePath, cancellationToken).ConfigureAwait(false);
            
            var encoder = GetEncoder(format, quality);
            await sourceImage.SaveAsync(outputPath, encoder, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Saved image: {OutputPath} (Format: {Format}, Quality: {Quality})", outputPath, format, quality);
        }

        public async Task ConvertImageFormatAsync(string inputPath, string outputPath, Models.ImageFormat targetFormat, int quality = 95, CancellationToken cancellationToken = default)
        {
            using var image = await Image.LoadAsync<Rgba32>(inputPath, cancellationToken).ConfigureAwait(false);
            
            var encoder = GetEncoder(targetFormat, quality);
            await image.SaveAsync(outputPath, encoder, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Converted image format: {InputPath} -> {OutputPath} ({Format})", inputPath, outputPath, targetFormat);
        }

        public Task<bool> IsValidImageFormatAsync(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp" };
                return Task.FromResult(supportedExtensions.Contains(extension));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public async Task<(int Width, int Height)> GetImageDimensionsAsync(string imagePath)
        {
            var image = await Image.IdentifyAsync(imagePath).ConfigureAwait(false);
            return (image.Width, image.Height);
        }

        // Private helper methods

        private async Task<ColorInfo> CalculateAverageColorAsync(Image<Rgba32> image, CancellationToken cancellationToken)
        {
            await Task.Yield(); // Make async
            
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
            await Task.Yield(); // Make async

            var colorCounts = new Dictionary<ColorInfo, int>();
            long totalBrightness = 0;
            var pixelCount = image.Width * image.Height;

            // Sample analysis (for performance, we don't analyze every pixel for dominant color)
            var sampleRate = Math.Max(1, pixelCount / 10000); // Sample ~10k pixels max

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
            // Simplified contrast calculation
            var brightnesses = new List<double>();
            var sampleRate = Math.Max(1, (image.Width * image.Height) / 1000); // Sample 1k pixels

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
            return Math.Sqrt(variance); // Standard deviation as contrast measure
        }

        private async Task<byte[]> CreateThumbnailBytesAsync(Image<Rgba32> image, int maxWidth, int maxHeight, CancellationToken cancellationToken)
        {
            using var thumbnail = image.Clone();
            thumbnail.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxWidth, maxHeight)
            }));

            using var stream = new MemoryStream();
            var encoder = new JpegEncoder { Quality = 80 };
            await thumbnail.SaveAsync(stream, encoder, cancellationToken).ConfigureAwait(false);
            return stream.ToArray();
        }

        private Models.ImageFormat GetImageFormatFromPath(string imagePath)
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

        private IImageEncoder GetEncoder(Models.ImageFormat format, int quality)
        {
            return format switch
            {
                Models.ImageFormat.Jpeg => new JpegEncoder { Quality = quality },
                Models.ImageFormat.Png => new PngEncoder(),
                Models.ImageFormat.Webp => new WebpEncoder { Quality = quality },
                _ => new JpegEncoder { Quality = quality }
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _memorySemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}