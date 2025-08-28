using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Factory for creating appropriate mosaic services based on request type
    /// </summary>
    public class MosaicServiceFactory : IMosaicServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MosaicServiceFactory> _logger;

        public MosaicServiceFactory(IServiceProvider serviceProvider, ILogger<MosaicServiceFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates the appropriate mosaic service based on the request type
        /// </summary>
        public IMosaicService CreateService<TRequest>() where TRequest : MosaicRequestBase
        {
            return typeof(TRequest) switch
            {
                Type t when t == typeof(ColorMosaicRequest) => GetService<ColorMosaicService>(),
                Type t when t == typeof(HueMosaicRequest) => GetService<HueMosaicService>(),
                Type t when t == typeof(PhotoMosaicRequest) => GetService<PhotoMosaicService>(),
                _ => throw new ArgumentException($"Unsupported mosaic request type: {typeof(TRequest).Name}")
            };
        }

        /// <summary>
        /// Creates the appropriate mosaic service based on the request instance
        /// </summary>
        public IMosaicService CreateService(MosaicRequestBase request)
        {
            return request switch
            {
                ColorMosaicRequest => GetService<ColorMosaicService>(),
                HueMosaicRequest => GetService<HueMosaicService>(),
                PhotoMosaicRequest => GetService<PhotoMosaicService>(),
                _ => throw new ArgumentException($"Unsupported mosaic request type: {request.GetType().Name}")
            };
        }

        /// <summary>
        /// Gets all available mosaic service types
        /// </summary>
        public Type[] GetSupportedRequestTypes()
        {
            return new[]
            {
                typeof(ColorMosaicRequest),
                typeof(HueMosaicRequest),
                typeof(PhotoMosaicRequest)
            };
        }

        /// <summary>
        /// Gets service names for the supported request types
        /// </summary>
        public string[] GetSupportedServiceNames()
        {
            return new[]
            {
                "ColorMosaic",
                "HueMosaic",
                "PhotoMosaic"
            };
        }

        private TService GetService<TService>() where TService : class, IMosaicService
        {
            var service = _serviceProvider.GetService<TService>();
            if (service == null)
            {
                _logger.LogError("Failed to resolve service: {ServiceType}", typeof(TService).Name);
                throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered");
            }

            _logger.LogDebug("Created service: {ServiceType}", typeof(TService).Name);
            return service;
        }
    }

    /// <summary>
    /// Interface for the mosaic service factory
    /// </summary>
    public interface IMosaicServiceFactory
    {
        /// <summary>
        /// Creates the appropriate mosaic service based on the request type
        /// </summary>
        IMosaicService CreateService<TRequest>() where TRequest : MosaicRequestBase;

        /// <summary>
        /// Creates the appropriate mosaic service based on the request instance
        /// </summary>
        IMosaicService CreateService(MosaicRequestBase request);

        /// <summary>
        /// Gets all supported request types
        /// </summary>
        Type[] GetSupportedRequestTypes();

        /// <summary>
        /// Gets service names for the supported request types
        /// </summary>
        string[] GetSupportedServiceNames();
    }
}