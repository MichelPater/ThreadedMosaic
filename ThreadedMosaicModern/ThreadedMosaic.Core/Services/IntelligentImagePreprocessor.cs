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
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Intelligent image preprocessing service that optimizes images before processing
    /// Applies smart resizing, format conversion, and quality optimization based on image characteristics
    /// </summary>
    public class IntelligentImagePreprocessor : IDisposable
    {
        private readonly ILogger<IntelligentImagePreprocessor> _logger;
        private readonly IMemoryCache _preprocessingCache;
        private readonly SemaphoreSlim _processingLimiter;
        private readonly ImagePreprocessingOptions _options;
        private bool _disposed;

        public IntelligentImagePreprocessor(
            ILogger<IntelligentImagePreprocessor> logger,
            IMemoryCache memoryCache,
            ImagePreprocessingOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _preprocessingCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _options = options ?? ImagePreprocessingOptions.Default;
            _processingLimiter = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        }

        /// <summary>
        /// Intelligently preprocesses an image for optimal mosaic processing
        /// </summary>
        public async Task<PreprocessedImageResult> PreprocessImageAsync(
            string imagePath, 
            MosaicProcessingContext context,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            var cacheKey = GenerateCacheKey(imagePath, context);
            
            if (_preprocessingCache.TryGetValue(cacheKey, out PreprocessedImageResult? cached) && cached != null)
            {
                _logger.LogDebug("Retrieved preprocessed image from cache: {ImagePath}", imagePath);
                return cached;
            }

            await _processingLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            try
            {
                return await PerformPreprocessingAsync(imagePath, context, cacheKey, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        /// <summary>
        /// Batch preprocesses multiple images with intelligent optimization
        /// </summary>
        public async Task<IEnumerable<PreprocessedImageResult>> PreprocessBatchAsync(
            IEnumerable<string> imagePaths,
            MosaicProcessingContext context,
            IProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            var imageList = imagePaths.ToList();
            var results = new List<PreprocessedImageResult>();

            if (progressReporter != null)
            {
                await progressReporter.SetMaximumAsync(imageList.Count, cancellationToken).ConfigureAwait(false);
            }

            // Analyze batch characteristics for optimization
            var batchProfile = await AnalyzeBatchCharacteristicsAsync(imageList, cancellationToken).ConfigureAwait(false);
            var optimizedContext = OptimizeContextForBatch(context, batchProfile);

            _logger.LogInformation("Preprocessing batch of {Count} images with profile: {Profile}", 
                imageList.Count, batchProfile);

            var processingTasks = imageList.Select(async (imagePath, index) =>
            {
                try
                {
                    var result = await PreprocessImageAsync(imagePath, optimizedContext, cancellationToken).ConfigureAwait(false);
                    
                    if (progressReporter != null)
                    {
                        await progressReporter.ReportProgressAsync(
                            index + 1,
                            imageList.Count,
                            $"Preprocessed {Path.GetFileName(imagePath)}",
                            cancellationToken).ConfigureAwait(false);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to preprocess image: {ImagePath}", imagePath);
                    return new PreprocessedImageResult
                    {
                        OriginalPath = imagePath,
                        IsOptimized = false,
                        ErrorMessage = ex.Message
                    };
                }
            });

            var allResults = await Task.WhenAll(processingTasks).ConfigureAwait(false);
            results.AddRange(allResults.Where(r => r.IsOptimized || !string.IsNullOrEmpty(r.ErrorMessage)));

            _logger.LogInformation("Batch preprocessing completed: {Success}/{Total} images processed successfully",
                results.Count(r => r.IsOptimized), imageList.Count);

            return results;
        }

        /// <summary>
        /// Optimizes image dimensions for mosaic processing
        /// </summary>
        public async Task<PreprocessedImageResult> OptimizeDimensionsAsync(
            string imagePath,
            int targetWidth,
            int targetHeight,
            ResizeStrategy strategy = ResizeStrategy.Smart,
            CancellationToken cancellationToken = default)
        {
            using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
            
            var analysis = AnalyzeImageForResizing(image, targetWidth, targetHeight);
            var optimizedDimensions = CalculateOptimalDimensions(image.Width, image.Height, targetWidth, targetHeight, strategy);

            _logger.LogDebug("Optimizing dimensions for {ImagePath}: {OriginalW}x{OriginalH} -> {NewW}x{NewH} (Strategy: {Strategy})",
                imagePath, image.Width, image.Height, optimizedDimensions.Width, optimizedDimensions.Height, strategy);

            if (optimizedDimensions.Width == image.Width && optimizedDimensions.Height == image.Height)
            {
                return new PreprocessedImageResult
                {
                    OriginalPath = imagePath,
                    OptimizedPath = imagePath,
                    IsOptimized = false,
                    OptimizationType = OptimizationType.None,
                    OriginalDimensions = (image.Width, image.Height),
                    OptimizedDimensions = (image.Width, image.Height)
                };
            }

            var optimizedPath = GenerateOptimizedPath(imagePath, "resized", optimizedDimensions);
            
            // Apply intelligent resampling based on resize ratio
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(optimizedDimensions.Width, optimizedDimensions.Height),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3 // Use high quality resampler
            }));

            await image.SaveAsync(optimizedPath, cancellationToken).ConfigureAwait(false);

            return new PreprocessedImageResult
            {
                OriginalPath = imagePath,
                OptimizedPath = optimizedPath,
                IsOptimized = true,
                OptimizationType = OptimizationType.Resized,
                OriginalDimensions = (File.Exists(imagePath) ? await GetImageDimensionsAsync(imagePath) : (0, 0)),
                OptimizedDimensions = optimizedDimensions,
                FileSizeReduction = CalculateFileSizeReduction(imagePath, optimizedPath),
                ProcessingTime = TimeSpan.FromMilliseconds(Environment.TickCount)
            };
        }

        private async Task<PreprocessedImageResult> PerformPreprocessingAsync(
            string imagePath,
            MosaicProcessingContext context,
            string cacheKey,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var fileInfo = new FileInfo(imagePath);
            
            _logger.LogDebug("Preprocessing image: {ImagePath} (Size: {Size:N0} bytes)", imagePath, fileInfo.Length);

            using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
            
            var analysis = await AnalyzeImageAsync(image, cancellationToken).ConfigureAwait(false);
            var optimizations = DetermineOptimizations(analysis, context);

            var result = new PreprocessedImageResult
            {
                OriginalPath = imagePath,
                OptimizedPath = imagePath,
                IsOptimized = false,
                OriginalDimensions = (image.Width, image.Height)
            };

            if (optimizations.Any())
            {
                var optimizedPath = await ApplyOptimizationsAsync(image, imagePath, optimizations, cancellationToken).ConfigureAwait(false);
                
                result.OptimizedPath = optimizedPath;
                result.IsOptimized = true;
                result.OptimizationType = GetCombinedOptimizationType(optimizations);
                result.OptimizedDimensions = await GetImageDimensionsAsync(optimizedPath);
                result.FileSizeReduction = CalculateFileSizeReduction(imagePath, optimizedPath);
            }

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.Normal,
                Size = 1
            };
            _preprocessingCache.Set(cacheKey, result, cacheOptions);

            _logger.LogInformation("Preprocessed {ImagePath}: {Optimized} (Optimizations: {Opts}, Time: {Time:F2}ms)",
                Path.GetFileName(imagePath), result.IsOptimized, string.Join(", ", optimizations), 
                result.ProcessingTime.TotalMilliseconds);

            return result;
        }

        private async Task<ImageProcessingAnalysis> AnalyzeImageAsync(Image<Rgba32> image, CancellationToken cancellationToken)
        {
            await Task.Yield();

            var pixelCount = image.Width * image.Height;
            var aspectRatio = (double)image.Width / image.Height;
            
            // Sample analysis for large images
            var sampleRate = Math.Max(1, pixelCount / 5000);
            var colorComplexity = 0.0;
            var edgePixels = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 1; y < accessor.Height - 1; y += sampleRate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var currentRow = accessor.GetRowSpan(y);
                    var prevRow = accessor.GetRowSpan(y - 1);
                    var nextRow = accessor.GetRowSpan(y + 1);

                    for (int x = 1; x < currentRow.Length - 1; x += sampleRate)
                    {
                        var current = currentRow[x];
                        
                        // Edge detection (simple Sobel approximation)
                        var gx = Math.Abs(prevRow[x - 1].R - prevRow[x + 1].R) +
                                Math.Abs(currentRow[x - 1].R - currentRow[x + 1].R) +
                                Math.Abs(nextRow[x - 1].R - nextRow[x + 1].R);
                        
                        if (gx > 30) edgePixels++;
                        
                        // Color variation
                        var neighbors = new[] {
                            prevRow[x - 1], prevRow[x], prevRow[x + 1],
                            currentRow[x - 1], currentRow[x + 1],
                            nextRow[x - 1], nextRow[x], nextRow[x + 1]
                        };
                        
                        var avgVariation = neighbors.Average(p => 
                            Math.Abs(p.R - current.R) + Math.Abs(p.G - current.G) + Math.Abs(p.B - current.B));
                        
                        colorComplexity += avgVariation;
                    }
                }
            });

            var sampledPixels = Math.Max(1, ((image.Width - 2) / sampleRate) * ((image.Height - 2) / sampleRate));
            
            return new ImageProcessingAnalysis
            {
                Width = image.Width,
                Height = image.Height,
                PixelCount = pixelCount,
                AspectRatio = aspectRatio,
                ColorComplexity = colorComplexity / sampledPixels,
                EdgeDensity = (double)edgePixels / sampledPixels,
                ResizeRatio = 1.0,
                EstimatedProcessingComplexity = CalculateProcessingComplexity(pixelCount, colorComplexity / sampledPixels, (double)edgePixels / sampledPixels)
            };
        }

        private List<ImageOptimization> DetermineOptimizations(ImageProcessingAnalysis analysis, MosaicProcessingContext context)
        {
            var optimizations = new List<ImageOptimization>();

            // Dimension optimization
            if (ShouldResizeForMosaic(analysis, context))
            {
                var optimalSize = CalculateOptimalMosaicSize(analysis, context);
                optimizations.Add(new ImageOptimization
                {
                    Type = OptimizationType.Resized,
                    TargetDimensions = optimalSize,
                    Parameters = new Dictionary<string, object> { { "Strategy", ResizeStrategy.Smart } }
                });
            }

            // Quality optimization based on complexity
            if (ShouldOptimizeQuality(analysis))
            {
                var optimalQuality = CalculateOptimalQuality(analysis);
                optimizations.Add(new ImageOptimization
                {
                    Type = OptimizationType.QualityOptimized,
                    Parameters = new Dictionary<string, object> { { "Quality", optimalQuality } }
                });
            }

            // Format optimization
            if (ShouldOptimizeFormat(analysis, context))
            {
                var optimalFormat = DetermineOptimalFormat(analysis);
                optimizations.Add(new ImageOptimization
                {
                    Type = OptimizationType.FormatOptimized,
                    Parameters = new Dictionary<string, object> { { "Format", optimalFormat } }
                });
            }

            return optimizations;
        }

        private async Task<string> ApplyOptimizationsAsync(
            Image<Rgba32> image, 
            string originalPath, 
            List<ImageOptimization> optimizations,
            CancellationToken cancellationToken)
        {
            var workingImage = image.Clone();
            var outputPath = originalPath;

            try
            {
                foreach (var optimization in optimizations)
                {
                    switch (optimization.Type)
                    {
                        case OptimizationType.Resized:
                            if (optimization.TargetDimensions.HasValue)
                            {
                                var dims = optimization.TargetDimensions.Value;
                                var strategy = (ResizeStrategy)optimization.Parameters.GetValueOrDefault("Strategy", ResizeStrategy.Smart);
                                workingImage.Mutate(x => x.Resize(new ResizeOptions
                                {
                                    Size = new Size(dims.Width, dims.Height),
                                    Mode = ResizeMode.Stretch,
                                    Sampler = KnownResamplers.Lanczos3
                                }));
                                
                                outputPath = GenerateOptimizedPath(originalPath, "resized", dims);
                            }
                            break;

                        case OptimizationType.QualityOptimized:
                            // Quality will be applied during save
                            break;

                        case OptimizationType.FormatOptimized:
                            var format = (Models.ImageFormat)optimization.Parameters.GetValueOrDefault("Format", Models.ImageFormat.Jpeg);
                            outputPath = Path.ChangeExtension(outputPath, GetExtensionForFormat(format));
                            break;
                    }
                }

                // Save optimized image - simplified for now
                await workingImage.SaveAsync(outputPath, cancellationToken).ConfigureAwait(false);
                
                return outputPath;
            }
            finally
            {
                workingImage.Dispose();
            }
        }

        // Helper methods for optimization logic

        private bool ShouldResizeForMosaic(ImageProcessingAnalysis analysis, MosaicProcessingContext context)
        {
            // Resize if image is much larger than mosaic requirements
            var maxDimension = Math.Max(analysis.Width, analysis.Height);
            var targetMaxDimension = Math.Max(context.TargetWidth ?? 1920, context.TargetHeight ?? 1080);
            
            return maxDimension > targetMaxDimension * 1.5 || analysis.PixelCount > _options.MaxPixelCountBeforeResize;
        }

        private (int Width, int Height) CalculateOptimalMosaicSize(ImageProcessingAnalysis analysis, MosaicProcessingContext context)
        {
            var targetWidth = context.TargetWidth ?? 1920;
            var targetHeight = context.TargetHeight ?? 1080;
            
            return CalculateOptimalDimensions(analysis.Width, analysis.Height, targetWidth, targetHeight, ResizeStrategy.Smart);
        }

        private bool ShouldOptimizeQuality(ImageProcessingAnalysis analysis)
        {
            // Lower quality for images with low complexity
            return analysis.ColorComplexity < _options.LowComplexityThreshold || 
                   analysis.EdgeDensity < _options.LowEdgeDensityThreshold;
        }

        private int CalculateOptimalQuality(ImageProcessingAnalysis analysis)
        {
            // Dynamic quality based on image characteristics
            var baseQuality = 85;
            
            if (analysis.ColorComplexity < _options.LowComplexityThreshold) baseQuality -= 15;
            if (analysis.EdgeDensity < _options.LowEdgeDensityThreshold) baseQuality -= 10;
            if (analysis.EstimatedProcessingComplexity < 0.3) baseQuality -= 5;
            
            return Math.Max(60, Math.Min(95, baseQuality));
        }

        private bool ShouldOptimizeFormat(ImageProcessingAnalysis analysis, MosaicProcessingContext context)
        {
            return context.OptimizeForSpeed || analysis.EstimatedProcessingComplexity > _options.HighComplexityThreshold;
        }

        private Models.ImageFormat DetermineOptimalFormat(ImageProcessingAnalysis analysis)
        {
            // Use WebP for complex images with good compression
            if (analysis.ColorComplexity > _options.HighComplexityThreshold)
                return Models.ImageFormat.Webp;
            
            // Use JPEG for most cases
            return Models.ImageFormat.Jpeg;
        }

        private (int Width, int Height) CalculateOptimalDimensions(int originalWidth, int originalHeight, int targetWidth, int targetHeight, ResizeStrategy strategy)
        {
            return strategy switch
            {
                ResizeStrategy.Exact => (targetWidth, targetHeight),
                ResizeStrategy.Proportional => CalculateProportionalDimensions(originalWidth, originalHeight, targetWidth, targetHeight),
                ResizeStrategy.Smart => CalculateSmartDimensions(originalWidth, originalHeight, targetWidth, targetHeight),
                _ => (originalWidth, originalHeight)
            };
        }

        private (int Width, int Height) CalculateProportionalDimensions(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
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

        private (int Width, int Height) CalculateSmartDimensions(int originalWidth, int originalHeight, int targetWidth, int targetHeight)
        {
            // Smart resizing considers performance implications
            var pixelCount = originalWidth * originalHeight;
            var targetPixelCount = targetWidth * targetHeight;
            
            if (pixelCount <= targetPixelCount * 1.2)
                return (originalWidth, originalHeight); // Keep original if close enough
            
            return CalculateProportionalDimensions(originalWidth, originalHeight, targetWidth, targetHeight);
        }

        private static object SelectOptimalResampler(double resizeRatio)
        {
            return resizeRatio switch
            {
                < 0.5 => KnownResamplers.Triangle, // Fast for significant downscaling
                < 0.8 => KnownResamplers.Lanczos3, // High quality for moderate downscaling
                > 1.2 => KnownResamplers.Bicubic,  // Good for upscaling
                _ => KnownResamplers.Lanczos3      // General purpose
            };
        }

        private async Task<BatchProcessingProfile> AnalyzeBatchCharacteristicsAsync(List<string> imagePaths, CancellationToken cancellationToken)
        {
            var sampleSize = Math.Min(10, imagePaths.Count);
            var samplePaths = imagePaths.Take(sampleSize);
            
            var fileSizes = new List<long>();
            var dimensions = new List<(int Width, int Height)>();
            
            foreach (var path in samplePaths)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                try
                {
                    fileSizes.Add(new FileInfo(path).Length);
                    dimensions.Add(await GetImageDimensionsAsync(path));
                }
                catch
                {
                    // Skip problematic files in analysis
                }
            }
            
            return new BatchProcessingProfile
            {
                AverageFileSize = fileSizes.Any() ? fileSizes.Average() : 0,
                AverageWidth = dimensions.Any() ? (int)dimensions.Average(d => d.Width) : 0,
                AverageHeight = dimensions.Any() ? (int)dimensions.Average(d => d.Height) : 0,
                TotalImages = imagePaths.Count,
                SampleAnalyzed = fileSizes.Count,
                EstimatedProcessingTime = EstimateBatchProcessingTime(imagePaths.Count, fileSizes.Any() ? fileSizes.Average() : 0)
            };
        }

        private MosaicProcessingContext OptimizeContextForBatch(MosaicProcessingContext original, BatchProcessingProfile profile)
        {
            var optimized = new MosaicProcessingContext
            {
                TargetWidth = original.TargetWidth,
                TargetHeight = original.TargetHeight,
                OptimizeForSpeed = original.OptimizeForSpeed || profile.EstimatedProcessingTime.TotalMinutes > 10,
                MaxMemoryUsage = original.MaxMemoryUsage,
                QualityPriority = original.QualityPriority
            };
            
            // Adjust targets based on batch characteristics
            if (profile.AverageWidth > 0 && profile.AverageHeight > 0)
            {
                optimized.TargetWidth ??= Math.Min(1920, profile.AverageWidth);
                optimized.TargetHeight ??= Math.Min(1080, profile.AverageHeight);
            }
            
            return optimized;
        }

        private TimeSpan EstimateBatchProcessingTime(int imageCount, double averageFileSize)
        {
            // Rough estimation based on file size and count
            var estimatedSecondsPerMB = 0.1;
            var averageFileSizeMB = averageFileSize / (1024 * 1024);
            var totalSeconds = imageCount * averageFileSizeMB * estimatedSecondsPerMB;
            
            return TimeSpan.FromSeconds(Math.Max(1, totalSeconds));
        }

        private double CalculateProcessingComplexity(int pixelCount, double colorComplexity, double edgePixelRatio)
        {
            var sizeComplexity = Math.Log10(pixelCount) / 7.0; // Normalize based on ~10M pixels = 1.0
            var visualComplexity = (colorComplexity / 100.0) * 0.5 + edgePixelRatio * 0.5;
            
            return Math.Min(1.0, sizeComplexity * 0.6 + visualComplexity * 0.4);
        }

        private ResizeImageAnalysis AnalyzeImageForResizing(Image image, int targetWidth, int targetHeight)
        {
            var originalPixels = image.Width * image.Height;
            var targetPixels = targetWidth * targetHeight;
            var resizeRatio = Math.Sqrt((double)targetPixels / originalPixels);
            
            return new ResizeImageAnalysis
            {
                OriginalDimensions = (image.Width, image.Height),
                TargetDimensions = (targetWidth, targetHeight),
                ResizeRatio = resizeRatio,
                IsUpscaling = resizeRatio > 1.0,
                IsSignificantResize = Math.Abs(resizeRatio - 1.0) > 0.2
            };
        }

        // Utility methods for file operations and caching

        private string GenerateCacheKey(string imagePath, MosaicProcessingContext context)
        {
            var fileInfo = new FileInfo(imagePath);
            var contextHash = context.GetHashCode();
            return $"preprocess_{Path.GetFileName(imagePath)}_{fileInfo.LastWriteTimeUtc.Ticks}_{contextHash}";
        }

        private string GenerateOptimizedPath(string originalPath, string suffix, (int Width, int Height) dimensions)
        {
            var directory = Path.GetDirectoryName(originalPath) ?? "";
            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            var extension = Path.GetExtension(originalPath);
            
            return Path.Combine(directory, $"{nameWithoutExt}_{suffix}_{dimensions.Width}x{dimensions.Height}{extension}");
        }

        private async Task<(int Width, int Height)> GetImageDimensionsAsync(string imagePath)
        {
            try
            {
                var info = await Image.IdentifyAsync(imagePath).ConfigureAwait(false);
                return (info.Width, info.Height);
            }
            catch
            {
                return (0, 0);
            }
        }

        private double CalculateFileSizeReduction(string originalPath, string optimizedPath)
        {
            try
            {
                var originalSize = new FileInfo(originalPath).Length;
                var optimizedSize = new FileInfo(optimizedPath).Length;
                
                return originalSize > 0 ? (double)(originalSize - optimizedSize) / originalSize : 0;
            }
            catch
            {
                return 0;
            }
        }

        private OptimizationType GetCombinedOptimizationType(List<ImageOptimization> optimizations)
        {
            var types = optimizations.Select(o => o.Type).Distinct().ToList();
            
            return types.Count switch
            {
                1 => types.First(),
                2 when types.Contains(OptimizationType.Resized) && types.Contains(OptimizationType.QualityOptimized) 
                    => OptimizationType.Resized | OptimizationType.QualityOptimized,
                _ => OptimizationType.Combined
            };
        }

        private int GetQualityFromOptimizations(List<ImageOptimization> optimizations)
        {
            var qualityOpt = optimizations.FirstOrDefault(o => o.Type == OptimizationType.QualityOptimized);
            return qualityOpt?.Parameters.GetValueOrDefault("Quality") as int? ?? 85;
        }

        private Models.ImageFormat GetImageFormatFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => Models.ImageFormat.Jpeg,
                ".png" => Models.ImageFormat.Png,
                ".webp" => Models.ImageFormat.Webp,
                ".bmp" => Models.ImageFormat.Bmp,
                ".gif" => Models.ImageFormat.Gif,
                ".tiff" or ".tif" => Models.ImageFormat.Tiff,
                _ => Models.ImageFormat.Jpeg
            };
        }

        private string GetExtensionForFormat(Models.ImageFormat format)
        {
            return format switch
            {
                Models.ImageFormat.Jpeg => ".jpg",
                Models.ImageFormat.Png => ".png",
                Models.ImageFormat.Webp => ".webp",
                Models.ImageFormat.Bmp => ".bmp",
                Models.ImageFormat.Gif => ".gif",
                Models.ImageFormat.Tiff => ".tif",
                _ => ".jpg"
            };
        }

        private IImageEncoder GetEncoderForFormat(Models.ImageFormat format, int quality)
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

    // Supporting classes and enums for the preprocessing service

    public class ImagePreprocessingOptions
    {
        public static ImagePreprocessingOptions Default => new()
        {
            MaxPixelCountBeforeResize = 8_000_000, // 8MP
            LowComplexityThreshold = 15.0,
            LowEdgeDensityThreshold = 0.1,
            HighComplexityThreshold = 50.0,
            EnableAggressiveOptimization = false
        };

        public int MaxPixelCountBeforeResize { get; set; } = 8_000_000;
        public double LowComplexityThreshold { get; set; } = 15.0;
        public double LowEdgeDensityThreshold { get; set; } = 0.1;
        public double HighComplexityThreshold { get; set; } = 50.0;
        public bool EnableAggressiveOptimization { get; set; } = false;
    }

    public class PreprocessedImageResult
    {
        public string OriginalPath { get; set; } = "";
        public string OptimizedPath { get; set; } = "";
        public bool IsOptimized { get; set; }
        public OptimizationType OptimizationType { get; set; }
        public (int Width, int Height) OriginalDimensions { get; set; }
        public (int Width, int Height) OptimizedDimensions { get; set; }
        public double FileSizeReduction { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class MosaicProcessingContext
    {
        public int? TargetWidth { get; set; }
        public int? TargetHeight { get; set; }
        public bool OptimizeForSpeed { get; set; }
        public long? MaxMemoryUsage { get; set; }
        public QualityPriority QualityPriority { get; set; } = QualityPriority.Balanced;
    }

    public class ImageProcessingAnalysis
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int PixelCount { get; set; }
        public double AspectRatio { get; set; }
        public double ColorComplexity { get; set; }
        public double EdgeDensity { get; set; }
        public double ResizeRatio { get; set; }
        public double EstimatedProcessingComplexity { get; set; }
    }

    public class ImageOptimization
    {
        public OptimizationType Type { get; set; }
        public (int Width, int Height)? TargetDimensions { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class BatchProcessingProfile
    {
        public double AverageFileSize { get; set; }
        public int AverageWidth { get; set; }
        public int AverageHeight { get; set; }
        public int TotalImages { get; set; }
        public int SampleAnalyzed { get; set; }
        public TimeSpan EstimatedProcessingTime { get; set; }
    }

    public class ResizeImageAnalysis
    {
        public (int Width, int Height) OriginalDimensions { get; set; }
        public (int Width, int Height) TargetDimensions { get; set; }
        public double ResizeRatio { get; set; }
        public bool IsUpscaling { get; set; }
        public bool IsSignificantResize { get; set; }
    }

    [Flags]
    public enum OptimizationType
    {
        None = 0,
        Resized = 1,
        QualityOptimized = 2,
        FormatOptimized = 4,
        Combined = 8
    }

    public enum ResizeStrategy
    {
        Exact,
        Proportional,
        Smart
    }

    public enum QualityPriority
    {
        Speed,
        Balanced,
        Quality
    }
}