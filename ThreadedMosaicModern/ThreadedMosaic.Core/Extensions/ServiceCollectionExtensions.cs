using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.Services;

namespace ThreadedMosaic.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuring ThreadedMosaic Core services in DI container
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all ThreadedMosaic Core services to the service collection
        /// </summary>
        public static IServiceCollection AddThreadedMosaicCore(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure settings
            services.Configure<MosaicConfiguration>(configuration.GetSection("MosaicConfiguration"));

            // Register core interfaces and implementations
            services.AddScoped<IImageProcessingService, ImageSharpProcessingService>();
            services.AddScoped<IFileOperations, ModernFileOperations>();
            services.AddSingleton<DefaultFileOperations>();
            services.AddSingleton<GlobalExceptionHandler>();

            // Register mosaic services
            services.AddScoped<ColorMosaicService>();
            services.AddScoped<HueMosaicService>();
            services.AddScoped<PhotoMosaicService>();
            services.AddScoped<IMosaicServiceFactory, MosaicServiceFactory>();

            // Register progress reporters
            services.AddTransient<ConsoleProgressReporter>();
            services.AddTransient<SignalRProgressReporter>();
            services.TryAddSingleton<NullProgressReporter>(_ => NullProgressReporter.Instance);

            // Add memory cache if not already registered
            services.AddMemoryCache();

            // Register background services and resource management
            services.AddHostedService<TempFileCleanupService>();
            services.AddSingleton<MemoryManagementService>();
            services.AddHostedService<MemoryManagementService>(provider => provider.GetRequiredService<MemoryManagementService>());
            services.AddSingleton<ConcurrentProcessingThrottleService>();

            return services;
        }

        /// <summary>
        /// Adds ThreadedMosaic Core services with a custom configuration action
        /// </summary>
        public static IServiceCollection AddThreadedMosaicCore(
            this IServiceCollection services,
            Action<MosaicConfiguration> configureOptions)
        {
            services.Configure(configureOptions);

            return services.AddThreadedMosaicCoreServices();
        }

        /// <summary>
        /// Adds only the core services without configuration
        /// </summary>
        private static IServiceCollection AddThreadedMosaicCoreServices(this IServiceCollection services)
        {
            // Register core interfaces and implementations
            services.AddScoped<IImageProcessingService, ImageSharpProcessingService>();
            services.AddScoped<IFileOperations, ModernFileOperations>();
            services.AddSingleton<DefaultFileOperations>();
            services.AddSingleton<GlobalExceptionHandler>();

            // Register mosaic services
            services.AddScoped<ColorMosaicService>();
            services.AddScoped<HueMosaicService>();
            services.AddScoped<PhotoMosaicService>();
            services.AddScoped<IMosaicServiceFactory, MosaicServiceFactory>();

            // Register progress reporters
            services.AddTransient<ConsoleProgressReporter>();
            services.AddTransient<SignalRProgressReporter>();
            services.TryAddSingleton<NullProgressReporter>(_ => NullProgressReporter.Instance);

            // Add memory cache if not already registered
            services.AddMemoryCache();

            return services;
        }

        /// <summary>
        /// Adds custom progress reporter implementation
        /// </summary>
        public static IServiceCollection AddProgressReporter<TProgressReporter>(
            this IServiceCollection services)
            where TProgressReporter : class, IProgressReporter
        {
            services.AddScoped<IProgressReporter, TProgressReporter>();
            return services;
        }

        /// <summary>
        /// Adds custom file operations implementation
        /// </summary>
        public static IServiceCollection AddFileOperations<TFileOperations>(
            this IServiceCollection services)
            where TFileOperations : class, IFileOperations
        {
            services.RemoveAll<IFileOperations>();
            services.AddScoped<IFileOperations, TFileOperations>();
            return services;
        }

        /// <summary>
        /// Adds custom image processing service implementation
        /// </summary>
        public static IServiceCollection AddImageProcessingService<TImageProcessingService>(
            this IServiceCollection services)
            where TImageProcessingService : class, IImageProcessingService
        {
            services.RemoveAll<IImageProcessingService>();
            services.AddScoped<IImageProcessingService, TImageProcessingService>();
            return services;
        }
    }
}