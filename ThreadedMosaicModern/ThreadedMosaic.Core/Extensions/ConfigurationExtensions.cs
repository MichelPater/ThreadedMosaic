using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuration validation and setup
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Validates the MosaicConfiguration and throws detailed errors if invalid
        /// </summary>
        public static MosaicConfiguration ValidateAndGet(this IConfiguration configuration, string sectionName = "MosaicConfiguration")
        {
            var config = configuration.GetSection(sectionName).Get<MosaicConfiguration>();
            if (config == null)
            {
                throw new InvalidOperationException($"Configuration section '{sectionName}' not found or is empty");
            }

            ValidateConfiguration(config);
            return config;
        }

        /// <summary>
        /// Gets validated configuration with fallbacks for missing values
        /// </summary>
        public static MosaicConfiguration GetWithDefaults(this IConfiguration configuration, string sectionName = "MosaicConfiguration")
        {
            var config = configuration.GetSection(sectionName).Get<MosaicConfiguration>() ?? new MosaicConfiguration();
            
            // Apply defaults for missing configuration
            ApplyDefaults(config);
            ValidateConfiguration(config);
            
            return config;
        }

        /// <summary>
        /// Configures options with validation
        /// </summary>
        public static IServiceCollection ConfigureMosaicOptions(
            this IServiceCollection services, 
            IConfiguration configuration, 
            string sectionName = "MosaicConfiguration")
        {
            services.Configure<MosaicConfiguration>(configuration.GetSection(sectionName));
            
            // Add options validation
            services.AddSingleton<IValidateOptions<MosaicConfiguration>, MosaicConfigurationValidator>();
            
            return services;
        }

        private static void ValidateConfiguration(MosaicConfiguration config)
        {
            if (config.ImageProcessing.MaxImageWidth <= 0 || config.ImageProcessing.MaxImageHeight <= 0)
            {
                throw new ArgumentException("MaxImageWidth and MaxImageHeight must be positive integers");
            }

            if (config.Performance.MaxConcurrentTasks < 0)
            {
                throw new ArgumentException("MaxConcurrentTasks cannot be negative");
            }

            if (config.FileManagement.MaxUploadFileSizeMB <= 0)
            {
                throw new ArgumentException("MaxUploadFileSizeMB must be positive");
            }

            if (!string.IsNullOrEmpty(config.FileManagement.TempDirectory))
            {
                try
                {
                    var fullPath = Path.GetFullPath(config.FileManagement.TempDirectory);
                    // Ensure parent directory exists or can be created
                    var parentDir = Path.GetDirectoryName(fullPath);
                    if (parentDir != null && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid TempDirectory path: {ex.Message}", ex);
                }
            }

            // Validate quality settings
            foreach (var quality in config.Quality.DefaultQuality.Values)
            {
                if (quality < 1 || quality > 100)
                {
                    throw new ArgumentException("Quality values must be between 1 and 100");
                }
            }
        }

        private static void ApplyDefaults(MosaicConfiguration config)
        {
            // Set default values if not configured
            if (config.Performance.MaxConcurrentTasks == 0)
            {
                config.Performance.MaxConcurrentTasks = Environment.ProcessorCount;
            }

            if (config.Performance.MaxDegreeOfParallelism == 0)
            {
                config.Performance.MaxDegreeOfParallelism = Environment.ProcessorCount;
            }

            if (string.IsNullOrEmpty(config.FileManagement.TempDirectory))
            {
                config.FileManagement.TempDirectory = Path.GetTempPath();
            }

            // Ensure quality settings exist for all formats
            if (!config.Quality.DefaultQuality.ContainsKey(ImageFormat.Jpeg))
                config.Quality.DefaultQuality[ImageFormat.Jpeg] = 85;
            if (!config.Quality.DefaultQuality.ContainsKey(ImageFormat.Png))
                config.Quality.DefaultQuality[ImageFormat.Png] = 100;
            if (!config.Quality.DefaultQuality.ContainsKey(ImageFormat.Webp))
                config.Quality.DefaultQuality[ImageFormat.Webp] = 80;
        }
    }

    /// <summary>
    /// Validator for MosaicConfiguration options
    /// </summary>
    public class MosaicConfigurationValidator : IValidateOptions<MosaicConfiguration>
    {
        public ValidateOptionsResult Validate(string? name, MosaicConfiguration options)
        {
            try
            {
                if (options == null)
                {
                    return ValidateOptionsResult.Fail("MosaicConfiguration cannot be null");
                }

                // Perform the same validation as the extension method
                if (options.ImageProcessing.MaxImageWidth <= 0 || options.ImageProcessing.MaxImageHeight <= 0)
                {
                    return ValidateOptionsResult.Fail("MaxImageWidth and MaxImageHeight must be positive integers");
                }

                if (options.Performance.MaxConcurrentTasks < 0)
                {
                    return ValidateOptionsResult.Fail("MaxConcurrentTasks cannot be negative");
                }

                if (options.FileManagement.MaxUploadFileSizeMB <= 0)
                {
                    return ValidateOptionsResult.Fail("MaxUploadFileSizeMB must be positive");
                }

                // Validate quality settings
                foreach (var quality in options.Quality.DefaultQuality.Values)
                {
                    if (quality < 1 || quality > 100)
                    {
                        return ValidateOptionsResult.Fail("Quality values must be between 1 and 100");
                    }
                }

                return ValidateOptionsResult.Success;
            }
            catch (Exception ex)
            {
                return ValidateOptionsResult.Fail($"Configuration validation failed: {ex.Message}");
            }
        }
    }
}