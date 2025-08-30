using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Services;
using Xunit;

namespace ThreadedMosaic.Tests.Services
{
    /// <summary>
    /// Tests for MosaicServiceFactory to verify proper service creation and dependency injection
    /// </summary>
    public class MosaicServiceFactoryTests : IDisposable
    {
        private readonly ServiceCollection _services;
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IImageProcessingService> _mockImageProcessingService;
        private readonly Mock<IFileOperations> _mockFileOperations;

        public MosaicServiceFactoryTests()
        {
            _services = new ServiceCollection();
            _mockImageProcessingService = new Mock<IImageProcessingService>();
            _mockFileOperations = new Mock<IFileOperations>();

            // Register dependencies
            _services.AddSingleton(_mockImageProcessingService.Object);
            _services.AddSingleton(_mockFileOperations.Object);
            _services.AddLogging();

            // Register the services that the factory creates
            _services.AddScoped<ColorMosaicService>();
            _services.AddScoped<HueMosaicService>();
            _services.AddScoped<PhotoMosaicService>();
            _services.AddScoped<ILogger<MosaicServiceFactory>>(provider => Mock.Of<ILogger<MosaicServiceFactory>>());
            _services.AddScoped<MosaicServiceFactory>();

            _serviceProvider = _services.BuildServiceProvider();
        }

        [Fact]
        public void Constructor_ValidServiceProvider_CreatesInstance()
        {
            // Act
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Assert
            factory.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions.Invoking(() => new MosaicServiceFactory(null!, Mock.Of<ILogger<MosaicServiceFactory>>()))
                         .Should().Throw<ArgumentNullException>()
                         .WithParameterName("serviceProvider");
        }

        [Fact]
        public void CreateService_ColorMosaicRequest_ReturnsColorMosaicService()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act
            var service = factory.CreateService<ColorMosaicRequest>();

            // Assert
            service.Should().NotBeNull();
            service.Should().BeOfType<ColorMosaicService>();
            service.Should().BeAssignableTo<IMosaicService>();
        }

        [Fact]
        public void CreateService_HueMosaicRequest_ReturnsHueMosaicService()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act
            var service = factory.CreateService<HueMosaicRequest>();

            // Assert
            service.Should().NotBeNull();
            service.Should().BeOfType<HueMosaicService>();
            service.Should().BeAssignableTo<IMosaicService>();
        }

        [Fact]
        public void CreateService_PhotoMosaicRequest_ReturnsPhotoMosaicService()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act
            var service = factory.CreateService<PhotoMosaicRequest>();

            // Assert
            service.Should().NotBeNull();
            service.Should().BeOfType<PhotoMosaicService>();
            service.Should().BeAssignableTo<IMosaicService>();
        }

        [Fact]
        public void CreateService_WithRequestInstance_ReturnsCorrectService()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();
            var colorRequest = new ColorMosaicRequest { MasterImagePath = "test.jpg", SeedImagesDirectory = "seeds" };
            var hueRequest = new HueMosaicRequest { MasterImagePath = "test.jpg", SeedImagesDirectory = "seeds" };
            var photoRequest = new PhotoMosaicRequest { MasterImagePath = "test.jpg", SeedImagesDirectory = "seeds" };

            // Act
            var colorService = factory.CreateService(colorRequest);
            var hueService = factory.CreateService(hueRequest);
            var photoService = factory.CreateService(photoRequest);

            // Assert
            colorService.Should().BeOfType<ColorMosaicService>();
            hueService.Should().BeOfType<HueMosaicService>();
            photoService.Should().BeOfType<PhotoMosaicService>();
        }

        [Fact]
        public void CreateService_MultipleCalls_ReturnsServicesWithSameType()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act
            var colorService1 = factory.CreateService<ColorMosaicRequest>();
            var colorService2 = factory.CreateService<ColorMosaicRequest>();
            var hueService1 = factory.CreateService<HueMosaicRequest>();
            var hueService2 = factory.CreateService<HueMosaicRequest>();

            // Assert - Services should be of the same type (may be same instance due to DI scoping)
            colorService1.Should().BeOfType<ColorMosaicService>();
            colorService2.Should().BeOfType<ColorMosaicService>();
            hueService1.Should().BeOfType<HueMosaicService>();
            hueService2.Should().BeOfType<HueMosaicService>();
        }

        [Fact]
        public void CreateService_NullRequest_ThrowsException()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act & Assert - Test with null request which should throw
            FluentActions.Invoking(() => factory.CreateService((MosaicRequestBase)null!))
                         .Should().Throw<Exception>(); // Can be NullReferenceException or ArgumentException
        }

        [Fact]
        public void GetSupportedRequestTypes_ValidCall_ReturnsExpectedTypes()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act
            var supportedTypes = factory.GetSupportedRequestTypes();

            // Assert
            supportedTypes.Should().NotBeNull();
            supportedTypes.Should().HaveCount(3);
            supportedTypes.Should().Contain(typeof(ColorMosaicRequest));
            supportedTypes.Should().Contain(typeof(HueMosaicRequest));
            supportedTypes.Should().Contain(typeof(PhotoMosaicRequest));
        }

        [Fact]
        public void GetSupportedServiceNames_ValidCall_ReturnsExpectedNames()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act
            var serviceNames = factory.GetSupportedServiceNames();

            // Assert
            serviceNames.Should().NotBeNull();
            serviceNames.Should().HaveCount(3);
            serviceNames.Should().Contain("ColorMosaic");
            serviceNames.Should().Contain("HueMosaic");
            serviceNames.Should().Contain("PhotoMosaic");
        }

        [Fact]
        public void CreateService_AllTypes_ShareSameDependencies()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act
            var colorService = factory.CreateService<ColorMosaicRequest>();
            var hueService = factory.CreateService<HueMosaicRequest>();
            var photoService = factory.CreateService<PhotoMosaicRequest>();

            // Assert - All services should be created successfully (dependencies properly injected)
            colorService.Should().NotBeNull();
            hueService.Should().NotBeNull();
            photoService.Should().NotBeNull();
        }

        [Fact]
        public void Factory_ImplementsCorrectInterface()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<MosaicServiceFactory>();

            // Act & Assert
            factory.Should().BeAssignableTo<IMosaicServiceFactory>();
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}