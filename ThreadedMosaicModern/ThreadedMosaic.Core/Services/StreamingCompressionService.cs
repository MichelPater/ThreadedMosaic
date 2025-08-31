using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    /// High-performance streaming compression service for large mosaic results
    /// Provides progressive compression, chunked streaming, and adaptive quality control
    /// </summary>
    public class StreamingCompressionService : IDisposable
    {
        private readonly ILogger<StreamingCompressionService> _logger;
        private readonly SemaphoreSlim _compressionLimiter;
        private readonly ArrayPool<byte> _bufferPool;
        private readonly CompressionOptions _options;
        private bool _disposed;

        private const int DEFAULT_CHUNK_SIZE = 1024 * 1024; // 1MB chunks
        private const int MIN_COMPRESSION_THRESHOLD = 5 * 1024 * 1024; // 5MB

        public StreamingCompressionService(
            ILogger<StreamingCompressionService> logger,
            CompressionOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? CompressionOptions.Default;
            _bufferPool = ArrayPool<byte>.Shared;
            _compressionLimiter = new SemaphoreSlim(Environment.ProcessorCount / 2, Environment.ProcessorCount);
        }

        /// <summary>
        /// Compresses and streams a large image with progressive quality
        /// </summary>
        public async Task<CompressedStreamResult> CompressImageStreamAsync(
            string imagePath,
            Stream outputStream,
            ProgressiveCompressionOptions compressionOptions,
            IProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            var fileInfo = new FileInfo(imagePath);
            var shouldCompress = fileInfo.Length >= MIN_COMPRESSION_THRESHOLD;

            _logger.LogInformation("Compressing image stream: {ImagePath} (Size: {Size:N0} bytes, Compress: {Compress})",
                imagePath, fileInfo.Length, shouldCompress);

            await _compressionLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                if (shouldCompress)
                {
                    return await CompressWithProgressiveQualityAsync(imagePath, outputStream, compressionOptions, progressReporter, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return await StreamWithoutCompressionAsync(imagePath, outputStream, progressReporter, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _compressionLimiter.Release();
            }
        }

        /// <summary>
        /// Creates a chunked compressed stream for progressive loading
        /// </summary>
        public async Task<ChunkedStreamResult> CreateChunkedStreamAsync(
            string imagePath,
            int chunkSize = DEFAULT_CHUNK_SIZE,
            CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(imagePath);
            var chunks = new List<CompressedChunk>();
            var totalCompressedSize = 0L;

            _logger.LogInformation("Creating chunked stream: {ImagePath} (Chunk size: {ChunkSize:N0} bytes)", imagePath, chunkSize);

            using var inputStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize);
            var buffer = _bufferPool.Rent(chunkSize);

            try
            {
                int chunkIndex = 0;
                int bytesRead;

                while ((bytesRead = await inputStream.ReadAsync(buffer, 0, chunkSize, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    var chunk = await CompressChunkAsync(buffer, bytesRead, chunkIndex, cancellationToken).ConfigureAwait(false);
                    chunks.Add(chunk);
                    totalCompressedSize += chunk.CompressedSize;
                    chunkIndex++;
                }

                var compressionRatio = fileInfo.Length > 0 ? (double)totalCompressedSize / fileInfo.Length : 1.0;

                _logger.LogInformation("Created {ChunkCount} chunks with {Ratio:P1} compression ratio", chunks.Count, compressionRatio);

                return new ChunkedStreamResult
                {
                    OriginalPath = imagePath,
                    OriginalSize = fileInfo.Length,
                    TotalCompressedSize = totalCompressedSize,
                    ChunkCount = chunks.Count,
                    Chunks = chunks,
                    CompressionRatio = compressionRatio,
                    CreatedAt = DateTime.UtcNow
                };
            }
            finally
            {
                _bufferPool.Return(buffer);
            }
        }

        /// <summary>
        /// Streams an image with adaptive quality based on network conditions
        /// </summary>
        public async Task<AdaptiveStreamResult> StreamWithAdaptiveQualityAsync(
            string imagePath,
            Stream outputStream,
            NetworkConditions networkConditions,
            IProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            var adaptiveOptions = CalculateAdaptiveOptions(imagePath, networkConditions);
            
            _logger.LogInformation("Streaming with adaptive quality: {ImagePath} (Quality: {Quality}, Format: {Format})",
                imagePath, adaptiveOptions.Quality, adaptiveOptions.Format);

            using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
            
            var originalSize = new FileInfo(imagePath).Length;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Apply adaptive transformations
            if (adaptiveOptions.RequiresResize)
            {
                var newSize = CalculateAdaptiveSize(image.Width, image.Height, networkConditions);
                image.Mutate(x => x.Resize(newSize.Width, newSize.Height, KnownResamplers.Triangle));
            }

            // Stream with appropriate encoder
            var encoder = CreateAdaptiveEncoder(adaptiveOptions);
            var compressionStream = CreateCompressionStream(outputStream, adaptiveOptions.UseCompression);

            try
            {
                if (progressReporter != null)
                {
                    await progressReporter.UpdateStatusAsync("Streaming image with adaptive quality...", cancellationToken).ConfigureAwait(false);
                }

                await image.SaveAsync(compressionStream, encoder, cancellationToken).ConfigureAwait(false);
                await compressionStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();
                var streamedSize = compressionStream.Length;

                return new AdaptiveStreamResult
                {
                    OriginalPath = imagePath,
                    OriginalSize = originalSize,
                    StreamedSize = streamedSize,
                    Quality = adaptiveOptions.Quality,
                    Format = adaptiveOptions.Format,
                    CompressionRatio = originalSize > 0 ? (double)streamedSize / originalSize : 1.0,
                    ProcessingTime = stopwatch.Elapsed,
                    NetworkConditions = networkConditions
                };
            }
            finally
            {
                if (compressionStream != outputStream)
                    compressionStream.Dispose();
            }
        }

        /// <summary>
        /// Creates a progressive JPEG stream with multiple quality levels
        /// </summary>
        public async Task<ProgressiveStreamResult> CreateProgressiveStreamAsync(
            string imagePath,
            Stream outputStream,
            int[] qualityLevels,
            CancellationToken cancellationToken = default)
        {
            if (qualityLevels == null || qualityLevels.Length == 0)
                qualityLevels = new[] { 30, 60, 85 }; // Default progressive levels

            _logger.LogInformation("Creating progressive stream: {ImagePath} ({LevelCount} quality levels)", imagePath, qualityLevels.Length);

            using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
            var progressiveLayers = new List<ProgressiveLayer>();
            var totalSize = 0L;

            for (int i = 0; i < qualityLevels.Length; i++)
            {
                var quality = qualityLevels[i];
                using var layerStream = new MemoryStream();
                
                var encoder = new JpegEncoder 
                { 
                    Quality = quality
                };

                await image.SaveAsync(layerStream, encoder, cancellationToken).ConfigureAwait(false);
                var layerBytes = layerStream.ToArray();
                totalSize += layerBytes.Length;

                // Write layer to output stream with metadata
                await WriteProgressiveLayerAsync(outputStream, i, quality, layerBytes, cancellationToken).ConfigureAwait(false);

                progressiveLayers.Add(new ProgressiveLayer
                {
                    Index = i,
                    Quality = quality,
                    Size = layerBytes.Length,
                    CumulativeSize = totalSize
                });

                _logger.LogDebug("Created progressive layer {Index} with quality {Quality} ({Size:N0} bytes)", i, quality, layerBytes.Length);
            }

            return new ProgressiveStreamResult
            {
                OriginalPath = imagePath,
                TotalSize = totalSize,
                LayerCount = qualityLevels.Length,
                Layers = progressiveLayers,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Compresses mosaic result data with metadata
        /// </summary>
        public async Task<CompressedMosaicResult> CompressMosaicResultAsync(
            MosaicResult mosaicResult,
            CompressionLevel compressionLevel = CompressionLevel.Optimal,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Serialize metadata
            var metadataJson = JsonSerializer.Serialize(mosaicResult, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataJson);

            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, compressionLevel))
            {
                await gzipStream.WriteAsync(metadataBytes, 0, metadataBytes.Length, cancellationToken).ConfigureAwait(false);
            }

            var compressedData = compressedStream.ToArray();
            stopwatch.Stop();

            return new CompressedMosaicResult
            {
                OriginalResult = mosaicResult,
                CompressedMetadata = compressedData,
                OriginalMetadataSize = metadataBytes.Length,
                CompressedMetadataSize = compressedData.Length,
                CompressionRatio = (double)compressedData.Length / metadataBytes.Length,
                CompressionTime = stopwatch.Elapsed
            };
        }

        private async Task<CompressedStreamResult> CompressWithProgressiveQualityAsync(
            string imagePath,
            Stream outputStream,
            ProgressiveCompressionOptions options,
            IProgressReporter? progressReporter,
            CancellationToken cancellationToken)
        {
            using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken).ConfigureAwait(false);
            var originalSize = new FileInfo(imagePath).Length;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Start with lowest quality for immediate display
            var initialEncoder = new JpegEncoder { Quality = options.InitialQuality };
            using var compressionStream = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true);

            if (progressReporter != null)
            {
                await progressReporter.UpdateStatusAsync($"Compressing with {options.InitialQuality}% quality...", cancellationToken).ConfigureAwait(false);
            }

            await image.SaveAsync(compressionStream, initialEncoder, cancellationToken).ConfigureAwait(false);
            await compressionStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            var compressedSize = outputStream.Length;

            return new CompressedStreamResult
            {
                OriginalPath = imagePath,
                OriginalSize = originalSize,
                CompressedSize = compressedSize,
                CompressionRatio = originalSize > 0 ? (double)compressedSize / originalSize : 1.0,
                CompressionTime = stopwatch.Elapsed,
                Quality = options.InitialQuality
            };
        }

        private async Task<CompressedStreamResult> StreamWithoutCompressionAsync(
            string imagePath,
            Stream outputStream,
            IProgressReporter? progressReporter,
            CancellationToken cancellationToken)
        {
            var fileInfo = new FileInfo(imagePath);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var inputStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = _bufferPool.Rent(DEFAULT_CHUNK_SIZE);

            try
            {
                var totalBytes = 0L;
                var bytesToCopy = fileInfo.Length;
                int bytesRead;

                if (progressReporter != null)
                {
                    await progressReporter.SetMaximumAsync((int)bytesToCopy, cancellationToken).ConfigureAwait(false);
                }

                while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await outputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    totalBytes += bytesRead;

                    if (progressReporter != null)
                    {
                        await progressReporter.ReportProgressAsync((int)totalBytes, (int)bytesToCopy, "Streaming...", cancellationToken).ConfigureAwait(false);
                    }
                }

                stopwatch.Stop();

                return new CompressedStreamResult
                {
                    OriginalPath = imagePath,
                    OriginalSize = fileInfo.Length,
                    CompressedSize = totalBytes,
                    CompressionRatio = 1.0, // No compression
                    CompressionTime = stopwatch.Elapsed,
                    Quality = 100
                };
            }
            finally
            {
                _bufferPool.Return(buffer);
            }
        }

        private async Task<CompressedChunk> CompressChunkAsync(byte[] buffer, int length, int chunkIndex, CancellationToken cancellationToken)
        {
            using var inputStream = new MemoryStream(buffer, 0, length);
            using var outputStream = new MemoryStream();
            using var compressionStream = new DeflateStream(outputStream, CompressionLevel.Optimal);

            await inputStream.CopyToAsync(compressionStream, cancellationToken).ConfigureAwait(false);
            await compressionStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var compressedData = outputStream.ToArray();
            var compressionRatio = length > 0 ? (double)compressedData.Length / length : 1.0;

            return new CompressedChunk
            {
                Index = chunkIndex,
                OriginalSize = length,
                CompressedSize = compressedData.Length,
                CompressionRatio = compressionRatio,
                Data = compressedData
            };
        }

        private AdaptiveCompressionOptions CalculateAdaptiveOptions(string imagePath, NetworkConditions networkConditions)
        {
            var fileSize = new FileInfo(imagePath).Length;
            var sizeMB = fileSize / (1024.0 * 1024.0);

            return new AdaptiveCompressionOptions
            {
                Quality = networkConditions.BandwidthMbps switch
                {
                    < 1 => 60,      // Low bandwidth
                    < 5 => 75,      // Medium bandwidth  
                    < 25 => 85,     // High bandwidth
                    _ => 90         // Very high bandwidth
                },
                Format = networkConditions.BandwidthMbps < 2 && sizeMB > 5 ? Models.ImageFormat.Webp : Models.ImageFormat.Jpeg,
                RequiresResize = sizeMB > 10 && networkConditions.BandwidthMbps < 10,
                UseCompression = networkConditions.BandwidthMbps < 5 || sizeMB > 20
            };
        }

        private (int Width, int Height) CalculateAdaptiveSize(int originalWidth, int originalHeight, NetworkConditions networkConditions)
        {
            var scaleFactor = networkConditions.BandwidthMbps switch
            {
                < 1 => 0.5,
                < 2 => 0.7,
                < 5 => 0.8,
                _ => 1.0
            };

            return (
                (int)(originalWidth * scaleFactor),
                (int)(originalHeight * scaleFactor)
            );
        }

        private IImageEncoder CreateAdaptiveEncoder(AdaptiveCompressionOptions options)
        {
            return options.Format switch
            {
                Models.ImageFormat.Webp => new WebpEncoder { Quality = options.Quality },
                Models.ImageFormat.Png => new PngEncoder(),
                _ => new JpegEncoder { Quality = options.Quality }
            };
        }

        private Stream CreateCompressionStream(Stream baseStream, bool useCompression)
        {
            return useCompression 
                ? new GZipStream(baseStream, CompressionLevel.Optimal, leaveOpen: true)
                : baseStream;
        }

        private async Task WriteProgressiveLayerAsync(Stream outputStream, int layerIndex, int quality, byte[] layerData, CancellationToken cancellationToken)
        {
            // Write layer metadata header
            var header = new ProgressiveLayerHeader
            {
                LayerIndex = layerIndex,
                Quality = quality,
                DataSize = layerData.Length,
                Checksum = CalculateChecksum(layerData)
            };

            var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);
            var headerSize = headerBytes.Length;

            // Write header size (4 bytes) + header + layer data
            await outputStream.WriteAsync(BitConverter.GetBytes(headerSize), 0, 4, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAsync(layerData, 0, layerData.Length, cancellationToken).ConfigureAwait(false);
        }

        private uint CalculateChecksum(byte[] data)
        {
            // Simple CRC32-like checksum
            uint checksum = 0xFFFFFFFF;
            foreach (var b in data)
            {
                checksum ^= b;
                for (int i = 0; i < 8; i++)
                {
                    checksum = (checksum & 1) == 1 ? (checksum >> 1) ^ 0xEDB88320 : checksum >> 1;
                }
            }
            return checksum ^ 0xFFFFFFFF;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _compressionLimiter?.Dispose();
                _disposed = true;
            }
        }
    }

    // Supporting classes for compression operations

    public class CompressionOptions
    {
        public static CompressionOptions Default => new()
        {
            DefaultQuality = 85,
            ChunkSize = 1024 * 1024,
            UseProgressiveJpeg = true,
            EnableAdaptiveCompression = true
        };

        public int DefaultQuality { get; set; } = 85;
        public int ChunkSize { get; set; } = 1024 * 1024;
        public bool UseProgressiveJpeg { get; set; } = true;
        public bool EnableAdaptiveCompression { get; set; } = true;
    }

    public class ProgressiveCompressionOptions
    {
        public int InitialQuality { get; set; } = 30;
        public int FinalQuality { get; set; } = 85;
        public int QualitySteps { get; set; } = 3;
        public TimeSpan LayerDelay { get; set; } = TimeSpan.FromSeconds(1);
    }

    public class NetworkConditions
    {
        public double BandwidthMbps { get; set; }
        public int LatencyMs { get; set; }
        public double PacketLoss { get; set; }
        public bool IsMobile { get; set; }
    }

    public class AdaptiveCompressionOptions
    {
        public int Quality { get; set; }
        public Models.ImageFormat Format { get; set; }
        public bool RequiresResize { get; set; }
        public bool UseCompression { get; set; }
    }

    public class CompressedStreamResult
    {
        public string OriginalPath { get; set; } = "";
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }
        public double CompressionRatio { get; set; }
        public TimeSpan CompressionTime { get; set; }
        public int Quality { get; set; }
    }

    public class ChunkedStreamResult
    {
        public string OriginalPath { get; set; } = "";
        public long OriginalSize { get; set; }
        public long TotalCompressedSize { get; set; }
        public int ChunkCount { get; set; }
        public List<CompressedChunk> Chunks { get; set; } = new();
        public double CompressionRatio { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CompressedChunk
    {
        public int Index { get; set; }
        public int OriginalSize { get; set; }
        public int CompressedSize { get; set; }
        public double CompressionRatio { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public class AdaptiveStreamResult
    {
        public string OriginalPath { get; set; } = "";
        public long OriginalSize { get; set; }
        public long StreamedSize { get; set; }
        public int Quality { get; set; }
        public Models.ImageFormat Format { get; set; }
        public double CompressionRatio { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public NetworkConditions NetworkConditions { get; set; } = new();
    }

    public class ProgressiveStreamResult
    {
        public string OriginalPath { get; set; } = "";
        public long TotalSize { get; set; }
        public int LayerCount { get; set; }
        public List<ProgressiveLayer> Layers { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ProgressiveLayer
    {
        public int Index { get; set; }
        public int Quality { get; set; }
        public long Size { get; set; }
        public long CumulativeSize { get; set; }
    }

    public class ProgressiveLayerHeader
    {
        public int LayerIndex { get; set; }
        public int Quality { get; set; }
        public int DataSize { get; set; }
        public uint Checksum { get; set; }
    }

    public class CompressedMosaicResult
    {
        public MosaicResult OriginalResult { get; set; } = new();
        public byte[] CompressedMetadata { get; set; } = Array.Empty<byte>();
        public int OriginalMetadataSize { get; set; }
        public int CompressedMetadataSize { get; set; }
        public double CompressionRatio { get; set; }
        public TimeSpan CompressionTime { get; set; }
    }
}