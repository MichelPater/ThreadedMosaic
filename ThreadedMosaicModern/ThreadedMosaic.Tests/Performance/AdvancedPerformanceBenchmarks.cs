using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using ThreadedMosaic.Core.Services;

namespace ThreadedMosaic.Tests.Performance
{
    /// <summary>
    /// Comprehensive performance benchmarks for advanced optimizations
    /// Validates memory-mapped files, intelligent preprocessing, and streaming compression
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob]
    [RankColumn]
    public class AdvancedPerformanceBenchmarks : IDisposable
    {
        private ImageSharpProcessingService? _standardService;
        private MemoryMappedImageProcessor? _memoryMappedProcessor;
        private IntelligentImagePreprocessor? _intelligentPreprocessor;
        private StreamingCompressionService? _streamingService;
        private PerformanceMonitoringService? _performanceMonitor;
        
        private string? _testDirectory;
        private string? _smallImagePath;
        private string? _largeImagePath;
        private string? _veryLargeImagePath;
        private IMemoryCache? _memoryCache;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "AdvancedBenchmarks", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            // Setup services
            var mockLogger = new Mock<ILogger<ImageSharpProcessingService>>();
            var mockMmLogger = new Mock<ILogger<MemoryMappedImageProcessor>>();
            var mockPrepLogger = new Mock<ILogger<IntelligentImagePreprocessor>>();
            var mockStreamLogger = new Mock<ILogger<StreamingCompressionService>>();
            var mockPerfLogger = new Mock<ILogger<PerformanceMonitoringService>>();
            
            _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
            
            _standardService = new ImageSharpProcessingService(_memoryCache, mockLogger.Object);
            _memoryMappedProcessor = new MemoryMappedImageProcessor(mockMmLogger.Object);
            _intelligentPreprocessor = new IntelligentImagePreprocessor(mockPrepLogger.Object, _memoryCache);
            _streamingService = new StreamingCompressionService(mockStreamLogger.Object);
            _performanceMonitor = new PerformanceMonitoringService(mockPerfLogger.Object);

            // Create test images of different sizes
            CreateTestImages();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _standardService?.Dispose();
            _memoryMappedProcessor?.Dispose();
            _intelligentPreprocessor?.Dispose();
            _streamingService?.Dispose();
            _performanceMonitor?.Dispose();
            _memoryCache?.Dispose();
            
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        // Standard vs Memory-Mapped Loading Benchmarks

        [Benchmark]
        [Arguments("small")]
        public async Task<ThreadedMosaic.Core.Models.LoadedImage> StandardImageLoading_Small()
        {
            return await _standardService!.LoadImageAsync(_smallImagePath!);
        }

        [Benchmark]
        [Arguments("large")]
        public async Task<ThreadedMosaic.Core.Models.LoadedImage> StandardImageLoading_Large()
        {
            return await _standardService!.LoadImageAsync(_largeImagePath!);
        }

        [Benchmark]
        [Arguments("small")]
        public async Task<ThreadedMosaic.Core.Models.LoadedImage> MemoryMappedLoading_Small()
        {
            return await _memoryMappedProcessor!.ProcessLargeImageAsync(_smallImagePath!);
        }

        [Benchmark]
        [Arguments("large")]
        public async Task<ThreadedMosaic.Core.Models.LoadedImage> MemoryMappedLoading_Large()
        {
            return await _memoryMappedProcessor!.ProcessLargeImageAsync(_largeImagePath!);
        }

        [Benchmark]
        [Arguments("verylarge")]
        public async Task<ThreadedMosaic.Core.Models.LoadedImage> MemoryMappedLoading_VeryLarge()
        {
            return await _memoryMappedProcessor!.ProcessLargeImageAsync(_veryLargeImagePath!);
        }

        // Thumbnail Generation Benchmarks

        [Benchmark]
        public async Task<byte[]> StandardThumbnailGeneration()
        {
            return await _standardService!.CreateThumbnailAsync(_largeImagePath!, 200, 200);
        }

        [Benchmark]
        public async Task<byte[]> StreamingThumbnailGeneration()
        {
            return await _memoryMappedProcessor!.CreateStreamingThumbnailAsync(_largeImagePath!, 200, 200);
        }

        // Image Preprocessing Benchmarks

        [Benchmark]
        public async Task<PreprocessedImageResult> IntelligentPreprocessing_Small()
        {
            var context = new MosaicProcessingContext
            {
                TargetWidth = 1920,
                TargetHeight = 1080,
                OptimizeForSpeed = false
            };
            return await _intelligentPreprocessor!.PreprocessImageAsync(_smallImagePath!, context);
        }

        [Benchmark]
        public async Task<PreprocessedImageResult> IntelligentPreprocessing_Large()
        {
            var context = new MosaicProcessingContext
            {
                TargetWidth = 1920,
                TargetHeight = 1080,
                OptimizeForSpeed = false
            };
            return await _intelligentPreprocessor!.PreprocessImageAsync(_largeImagePath!, context);
        }

        [Benchmark]
        public async Task<PreprocessedImageResult> IntelligentPreprocessing_SpeedOptimized()
        {
            var context = new MosaicProcessingContext
            {
                TargetWidth = 1920,
                TargetHeight = 1080,
                OptimizeForSpeed = true
            };
            return await _intelligentPreprocessor!.PreprocessImageAsync(_largeImagePath!, context);
        }

        // Batch Processing Benchmarks

        [Benchmark]
        public async Task<IEnumerable<PreprocessedImageResult>> BatchPreprocessing_SmallBatch()
        {
            var imagePaths = new[] { _smallImagePath!, _largeImagePath! };
            var context = new MosaicProcessingContext
            {
                TargetWidth = 1920,
                TargetHeight = 1080,
                OptimizeForSpeed = false
            };
            return await _intelligentPreprocessor!.PreprocessBatchAsync(imagePaths, context);
        }

        // Compression and Streaming Benchmarks

        [Benchmark]
        public async Task<CompressedStreamResult> StandardCompression()
        {
            using var outputStream = new MemoryStream();
            var options = new ProgressiveCompressionOptions
            {
                InitialQuality = 75,
                FinalQuality = 95
            };
            return await _streamingService!.CompressImageStreamAsync(_largeImagePath!, outputStream, options);
        }

        [Benchmark]
        public async Task<AdaptiveStreamResult> AdaptiveCompression_LowBandwidth()
        {
            using var outputStream = new MemoryStream();
            var networkConditions = new NetworkConditions
            {
                BandwidthMbps = 1.0, // Low bandwidth
                LatencyMs = 100,
                IsMobile = true
            };
            return await _streamingService!.StreamWithAdaptiveQualityAsync(_largeImagePath!, outputStream, networkConditions);
        }

        [Benchmark]
        public async Task<AdaptiveStreamResult> AdaptiveCompression_HighBandwidth()
        {
            using var outputStream = new MemoryStream();
            var networkConditions = new NetworkConditions
            {
                BandwidthMbps = 50.0, // High bandwidth
                LatencyMs = 20,
                IsMobile = false
            };
            return await _streamingService!.StreamWithAdaptiveQualityAsync(_largeImagePath!, outputStream, networkConditions);
        }

        [Benchmark]
        public async Task<ChunkedStreamResult> ChunkedStreaming()
        {
            return await _streamingService!.CreateChunkedStreamAsync(_veryLargeImagePath!, 1024 * 1024);
        }

        // Performance Monitoring Benchmarks

        [Benchmark]
        public void PerformanceTracking_SimpleOperation()
        {
            using var operation = _performanceMonitor!.StartOperation("benchmark_operation");
            operation.RecordProgress(50);
            operation.SetThroughput(100);
            operation.SetResult(true);
        }

        [Benchmark]
        public void PerformanceTracking_ComplexOperation()
        {
            using var operation = _performanceMonitor!.StartOperation("complex_benchmark", new Dictionary<string, object>
            {
                ["image_size"] = "large",
                ["processing_type"] = "compression"
            });
            
            operation.RecordImageMetrics(2048, 1536, 5 * 1024 * 1024);
            operation.RecordMemoryUsage(50 * 1024 * 1024);
            operation.RecordIOStats(5 * 1024 * 1024, 2 * 1024 * 1024);
            operation.SetThroughput(1000);
            operation.SetResult(true);
        }

        // Comparative Analysis Benchmarks

        [Benchmark]
        public async Task<double> StandardVsOptimized_ImageAnalysis()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var loadedImage = await _standardService!.LoadImageAsync(_largeImagePath!);
            var analysis = await _standardService.AnalyzeImageAsync(loadedImage);
            
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        [Benchmark]
        public async Task<double> MemoryMappedOptimized_ImageAnalysis()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var analysis = await _memoryMappedProcessor!.AnalyzeLargeImageAsync(_largeImagePath!);
            
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        // Memory Usage Comparison Benchmarks

        [Benchmark]
        public async Task<long> MeasureMemoryUsage_StandardLoading()
        {
            var initialMemory = GC.GetTotalMemory(true);
            
            var loadedImage = await _standardService!.LoadImageAsync(_veryLargeImagePath!);
            
            var finalMemory = GC.GetTotalMemory(false);
            return finalMemory - initialMemory;
        }

        [Benchmark]
        public async Task<long> MeasureMemoryUsage_MemoryMappedLoading()
        {
            var initialMemory = GC.GetTotalMemory(true);
            
            var loadedImage = await _memoryMappedProcessor!.ProcessLargeImageAsync(_veryLargeImagePath!);
            
            var finalMemory = GC.GetTotalMemory(false);
            return finalMemory - initialMemory;
        }

        // Real-world Scenario Benchmarks

        [Benchmark]
        public async Task<TimeSpan> CompleteImageProcessingPipeline_Standard()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Load image
            var loadedImage = await _standardService!.LoadImageAsync(_largeImagePath!);
            
            // Analyze colors
            var analysis = await _standardService.AnalyzeImageAsync(loadedImage);
            
            // Create thumbnail
            var thumbnail = await _standardService.CreateThumbnailAsync(_largeImagePath!, 200, 200);
            
            // Convert format
            var outputPath = Path.Combine(_testDirectory!, "output_standard.webp");
            await _standardService.ConvertImageFormatAsync(_largeImagePath!, outputPath, 
                ThreadedMosaic.Core.Models.ImageFormat.Webp, 85);
            
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        [Benchmark]
        public async Task<TimeSpan> CompleteImageProcessingPipeline_Optimized()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            using var perfTracker = _performanceMonitor!.StartOperation("optimized_pipeline");
            
            // Intelligent preprocessing
            var context = new MosaicProcessingContext
            {
                TargetWidth = 1920,
                TargetHeight = 1080,
                OptimizeForSpeed = true
            };
            var preprocessed = await _intelligentPreprocessor!.PreprocessImageAsync(_largeImagePath!, context);
            
            // Memory-mapped analysis
            var analysis = await _memoryMappedProcessor!.AnalyzeLargeImageAsync(_largeImagePath!);
            
            // Streaming thumbnail
            var thumbnail = await _memoryMappedProcessor!.CreateStreamingThumbnailAsync(_largeImagePath!, 200, 200);
            
            // Optimized format conversion
            var outputPath = Path.Combine(_testDirectory!, "output_optimized.webp");
            await _memoryMappedProcessor!.ConvertLargeImageAsync(_largeImagePath!, outputPath, 
                ThreadedMosaic.Core.Models.ImageFormat.Webp, 85);
            
            perfTracker.SetResult(true);
            
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        // Helper methods for test setup

        private void CreateTestImages()
        {
            _smallImagePath = Path.Combine(_testDirectory!, "small_image.png");
            _largeImagePath = Path.Combine(_testDirectory!, "large_image.png");
            _veryLargeImagePath = Path.Combine(_testDirectory!, "very_large_image.png");

            // Create 500x500 small image (~1.2MB)
            CreateTestImage(_smallImagePath, 500, 500);
            
            // Create 2048x1536 large image (~12MB)  
            CreateTestImage(_largeImagePath, 2048, 1536);
            
            // Create 4096x3072 very large image (~48MB)
            CreateTestImage(_veryLargeImagePath, 4096, 3072);
        }

        private void CreateTestImage(string path, int width, int height)
        {
            using var image = new Image<Rgb24>(width, height);

            // Create a complex pattern for realistic benchmarking
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        // Create a complex gradient pattern with noise
                        var r = (byte)((x * 255 / width) + Math.Sin(x * 0.1) * 50);
                        var g = (byte)((y * 255 / height) + Math.Cos(y * 0.1) * 50);
                        var b = (byte)(((x + y) * 255 / (width + height)) + Math.Sin(x * y * 0.001) * 30);
                        
                        row[x] = new Rgb24(r, g, b);
                    }
                }
            });

            image.Save(path);
        }

        public void Dispose()
        {
            GlobalCleanup();
        }
    }

    /// <summary>
    /// Benchmark runner with custom configuration for advanced performance testing
    /// </summary>
    public static class AdvancedBenchmarkRunner
    {
        public static void RunAllBenchmarks()
        {
            BenchmarkDotNet.Running.BenchmarkRunner.Run<AdvancedPerformanceBenchmarks>();
        }

        public static void RunMemoryBenchmarks()
        {
            BenchmarkDotNet.Running.BenchmarkRunner.Run<AdvancedPerformanceBenchmarks>(
                BenchmarkDotNet.Configs.DefaultConfig.Instance);
        }

        public static void RunCompressionBenchmarks()
        {
            // Run only compression-related benchmarks - simplified
            BenchmarkDotNet.Running.BenchmarkRunner.Run<AdvancedPerformanceBenchmarks>();
        }
    }

}