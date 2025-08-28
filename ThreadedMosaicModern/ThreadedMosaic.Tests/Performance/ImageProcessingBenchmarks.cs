using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThreadedMosaic.Core.Services;

namespace ThreadedMosaic.Tests.Performance
{
    /// <summary>
    /// Performance benchmarks for image processing operations
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob]
    public class ImageProcessingBenchmarks : IDisposable
    {
        private ImageSharpProcessingService? _service;
        private string? _testImagePath;
        private string? _testDirectory;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "ThreadedMosaicBenchmarks", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            // Setup service
            var mockLogger = new Mock<ILogger<ImageSharpProcessingService>>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            _service = new ImageSharpProcessingService(memoryCache, mockLogger.Object);

            // Create test image
            _testImagePath = CreateTestImage();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _service?.Dispose();
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Benchmark]
        public async Task<ThreadedMosaic.Core.Models.LoadedImage> LoadImage_Small()
        {
            return await _service!.LoadImageAsync(_testImagePath!);
        }

        [Benchmark]
        public async Task<byte[]> CreateThumbnail_150x150()
        {
            return await _service!.CreateThumbnailAsync(_testImagePath!, 150, 150);
        }

        [Benchmark]
        public async Task<(int Width, int Height)> GetImageDimensions()
        {
            return await _service!.GetImageDimensionsAsync(_testImagePath!);
        }

        [Benchmark]
        public async Task<bool> ValidateImageFormat()
        {
            return await _service!.IsValidImageFormatAsync(_testImagePath!);
        }

        [Benchmark]
        public async Task<ThreadedMosaic.Core.DTOs.ImageAnalysis> AnalyzeImage()
        {
            var loadedImage = await _service!.LoadImageAsync(_testImagePath!);
            return await _service!.AnalyzeImageAsync(loadedImage);
        }

        private string CreateTestImage()
        {
            var imagePath = Path.Combine(_testDirectory!, "benchmark_test.png");

            // Create a 100x100 test image
            using var image = new Image<Rgb24>(100, 100);

            // Fill with a pattern for more realistic benchmarking
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    // Create a simple pattern
                    var r = (byte)((x * 255) / image.Width);
                    var g = (byte)((y * 255) / image.Height);
                    var b = (byte)(((x + y) * 255) / (image.Width + image.Height));
                    image[x, y] = new Rgb24(r, g, b);
                }
            }

            image.Save(imagePath);
            return imagePath;
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }

    /// <summary>
    /// Simple benchmark runner for testing
    /// </summary>
    public static class BenchmarkRunner
    {
        public static void RunBenchmarks()
        {
            BenchmarkDotNet.Running.BenchmarkRunner.Run<ImageProcessingBenchmarks>();
        }
    }
}