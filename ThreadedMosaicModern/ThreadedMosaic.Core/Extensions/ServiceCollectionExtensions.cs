using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ThreadedMosaic.Core.Data;
using ThreadedMosaic.Core.Data.Repositories;
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

            // Add database services
            services.AddDatabaseServices(configuration);

            // Register core interfaces and implementations
            services.AddScoped<IImageProcessingService, ImageSharpProcessingService>();
            services.AddScoped<IFileOperations, ModernFileOperations>();
            services.AddSingleton<DefaultFileOperations>();
            services.AddSingleton<GlobalExceptionHandler>();

            // Register mosaic services
            services.AddScoped<ColorMosaicService>();
            services.AddScoped<IColorMosaicService>(provider => provider.GetRequiredService<ColorMosaicService>());
            services.AddScoped<HueMosaicService>();
            services.AddScoped<IHueMosaicService>(provider => provider.GetRequiredService<HueMosaicService>());
            services.AddScoped<PhotoMosaicService>();
            services.AddScoped<IPhotoMosaicService>(provider => provider.GetRequiredService<PhotoMosaicService>());
            services.AddScoped<IMosaicServiceFactory, MosaicServiceFactory>();

            // Register progress reporters
            services.AddTransient<ConsoleProgressReporter>();
            // services.AddTransient<SignalRProgressReporter>(); // Commented out for migration - requires ISignalRHubClient
            services.TryAddSingleton<NullProgressReporter>(_ => NullProgressReporter.Instance);

            // Add memory cache if not already registered
            services.AddMemoryCache();

            // Register background services and resource management
            services.AddHostedService<TempFileCleanupService>();
            services.AddSingleton<MemoryManagementService>();
            services.AddHostedService<MemoryManagementService>(provider => provider.GetRequiredService<MemoryManagementService>());
            services.AddSingleton<ConcurrentProcessingThrottleService>();
            
            // Register cancellation tracking service
            services.AddSingleton<IMosaicCancellationService, MosaicCancellationService>();

            // Add advanced performance optimization services
            services.AddAdvancedPerformanceServices();

            // Add health check services
            services.AddThreadedMosaicHealthChecks();

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
            services.AddScoped<IColorMosaicService>(provider => provider.GetRequiredService<ColorMosaicService>());
            services.AddScoped<HueMosaicService>();
            services.AddScoped<IHueMosaicService>(provider => provider.GetRequiredService<HueMosaicService>());
            services.AddScoped<PhotoMosaicService>();
            services.AddScoped<IPhotoMosaicService>(provider => provider.GetRequiredService<PhotoMosaicService>());
            services.AddScoped<IMosaicServiceFactory, MosaicServiceFactory>();

            // Register progress reporters
            services.AddTransient<ConsoleProgressReporter>();
            // services.AddTransient<SignalRProgressReporter>(); // Commented out for migration - requires ISignalRHubClient
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

        /// <summary>
        /// Adds database services to the service collection
        /// </summary>
        public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add DbContext with SQLite
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=threadedmosaic.db";

            services.AddDbContext<ThreadedMosaicDbContext>(options =>
            {
                options.UseSqlite(connectionString);
                options.EnableSensitiveDataLogging(false); // Disable in production
                options.EnableDetailedErrors(true);
            });

            // Add repositories
            services.AddScoped<IImageMetadataRepository, ImageMetadataRepository>();
            services.AddScoped<IMosaicProcessingResultRepository, MosaicProcessingResultRepository>();

            return services;
        }

        /// <summary>
        /// Ensures database is created and migrations are applied
        /// </summary>
        public static async Task<IServiceProvider> EnsureDatabaseCreatedAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ThreadedMosaicDbContext>();
            
            await dbContext.Database.MigrateAsync();
            
            return serviceProvider;
        }

        /// <summary>
        /// Adds advanced performance optimization services including memory-mapped processing, 
        /// intelligent preprocessing, streaming compression, and performance monitoring
        /// </summary>
        private static IServiceCollection AddAdvancedPerformanceServices(this IServiceCollection services)
        {
            // Memory-mapped file processing for large images
            services.AddScoped<MemoryMappedImageProcessor>();

            // Intelligent image preprocessing with adaptive quality
            services.AddScoped<IntelligentImagePreprocessor>();

            // Streaming compression services
            services.AddScoped<StreamingCompressionService>();

            // Performance monitoring and telemetry
            services.AddSingleton<PerformanceMonitoringService>();
            services.AddSingleton<IPerformanceMonitor>(provider => provider.GetRequiredService<PerformanceMonitoringService>());
            services.AddHostedService<PerformanceMonitoringService>(provider => provider.GetRequiredService<PerformanceMonitoringService>());

            return services;
        }

        /// <summary>
        /// Adds health check services for monitoring system health
        /// </summary>
        public static IServiceCollection AddThreadedMosaicHealthChecks(this IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddCheck<ThreadedMosaicHealthCheck>("system_resources", tags: new[] { "system" })
                .AddCheck<ImageProcessingHealthCheck>("image_processing", tags: new[] { "processing" })
                .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "database" });

            return services;
        }
    }
}