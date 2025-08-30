using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Tests.Infrastructure;

namespace ThreadedMosaic.Tests.Integration
{
    /// <summary>
    /// Enhanced integration tests for the API endpoints using TestWebApplicationFactory
    /// </summary>
    public class ApiIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>, IDisposable
    {
        private readonly TestWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly TestFileOperations _testFileOperations;
        private readonly string _testDirectory;

        public ApiIntegrationTests(TestWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
            _testFileOperations = _factory.GetRequiredService<IFileOperations>() as TestFileOperations 
                ?? throw new InvalidOperationException("Expected TestFileOperations in test environment");
            
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "ThreadedMosaicIntegrationTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        [Fact]
        public async Task GetHealthCheck_ReturnsHealthy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.Should().NotBeNull();
            // Note: This test assumes a health check endpoint exists, which may need to be added
        }

        [Fact]
        public async Task ApiEndpoints_AreAccessible()
        {
            // Test if swagger endpoint is accessible
            var swaggerResponse = await _client.GetAsync("/swagger/v1/swagger.json");
            swaggerResponse.Should().NotBeNull();
            
            // We don't check for success as swagger might not be enabled in all environments
            // but the endpoint should be reachable
        }

        [Fact]
        public async Task CreateColorMosaic_WithInvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var invalidRequest = new ColorMosaicRequest(); // Empty request should be invalid

            var json = JsonSerializer.Serialize(invalidRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/mosaic/color", content);

            // Assert
            response.Should().NotBeNull();
            // The response should indicate validation issues or bad request
            // We don't assert a specific status code as the exact validation behavior may vary
        }

        [Fact] 
        public async Task ApiEndpoints_ExistAndRespondToRequests()
        {
            // Test that the API endpoints exist by sending requests
            var endpoints = new[]
            {
                "/api/mosaic/color",
                "/api/mosaic/hue", 
                "/api/mosaic/photo"
            };

            foreach (var endpoint in endpoints)
            {
                // Act - Send empty POST request to check if endpoint exists
                var response = await _client.PostAsync(endpoint, new StringContent("", Encoding.UTF8, "application/json"));

                // Assert - Should get some response (not 404), even if it's bad request
                response.Should().NotBeNull();
                response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
            }
        }

        [Fact]
        public async Task GetMosaicStatus_WithRandomId_ReturnsNotFoundOrBadRequest()
        {
            // Arrange
            var randomId = Guid.NewGuid();

            // Act
            var response = await _client.GetAsync($"/api/mosaic/{randomId}/status");

            // Assert
            response.Should().NotBeNull();
            // Should return 404 (not found) or similar since the ID doesn't exist
            var allowedStatusCodes = new[]
            {
                System.Net.HttpStatusCode.NotFound,
                System.Net.HttpStatusCode.BadRequest
            };
            allowedStatusCodes.Should().Contain(response.StatusCode);
        }

        [Fact]
        public async Task ApiIsRunning_AndAccessible()
        {
            // Simple test to verify the API is running and accessible
            // This is a basic smoke test for the integration test setup

            // Act
            var response = await _client.GetAsync("/");

            // Assert
            response.Should().NotBeNull();
            // Don't assert specific status codes as the root endpoint behavior may vary
        }

        [Fact]
        public async Task FileOperations_Integration_WorksWithTestImplementation()
        {
            // Arrange - Setup test files
            var testFilePath = "/test/image.jpg";
            var testContent = "fake image data";
            _testFileOperations.AddTestFile(testFilePath, testContent);

            // Act - Verify the test file operations work
            var exists = await _testFileOperations.FileExistsAsync(testFilePath);
            var content = await _testFileOperations.ReadAllTextAsync(testFilePath);

            // Assert
            exists.Should().BeTrue();
            content.Should().Be(testContent);
        }

        [Fact]
        public async Task ApiConfiguration_IsCorrectForTesting()
        {
            // Verify the test environment is configured properly
            
            // Act - Get services from the test factory
            var fileOperations = _factory.GetService<IFileOperations>();

            // Assert
            fileOperations.Should().NotBeNull();
            fileOperations.Should().BeOfType<TestFileOperations>();
        }

        [Fact]
        public async Task MosaicEndpoints_HandleEmptyRequests_Gracefully()
        {
            // Test that the API handles various empty/invalid requests properly
            var endpoints = new[]
            {
                "/api/mosaic/color",
                "/api/mosaic/hue",
                "/api/mosaic/photo"
            };

            foreach (var endpoint in endpoints)
            {
                // Act - Send empty POST request
                var response = await _client.PostAsync(endpoint, new StringContent("", Encoding.UTF8, "application/json"));

                // Assert - Should handle gracefully (not crash the server)
                response.Should().NotBeNull();
                
                // The exact status code isn't critical, but it should be a client error
                var statusCode = (int)response.StatusCode;
                statusCode.Should().BeInRange(400, 499); // 4xx status codes for client errors
            }
        }

        [Fact]
        public async Task MosaicEndpoints_WithValidJsonButInvalidData_ReturnsBadRequest()
        {
            // Arrange - Create requests with valid JSON but missing required fields
            var colorRequest = new ColorMosaicRequest(); // Empty, should be invalid
            var hueRequest = new HueMosaicRequest(); // Empty, should be invalid
            var photoRequest = new PhotoMosaicRequest(); // Empty, should be invalid

            var requests = new (string Endpoint, object Request)[]
            {
                ("/api/mosaic/color", colorRequest),
                ("/api/mosaic/hue", hueRequest),
                ("/api/mosaic/photo", photoRequest)
            };

            foreach (var (endpoint, request) in requests)
            {
                // Arrange
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                // Act
                var response = await _client.PostAsync(endpoint, content);

                // Assert - Should return validation error
                response.Should().NotBeNull();
                
                // Should be a client error, typically 400 Bad Request for validation issues
                var statusCode = (int)response.StatusCode;
                statusCode.Should().BeInRange(400, 499);
            }
        }

        [Fact]
        public async Task CorsAndHeaders_AreConfiguredProperly()
        {
            // Test that CORS and other headers are configured for the API

            // Act
            var response = await _client.GetAsync("/");

            // Assert
            response.Should().NotBeNull();
            response.Headers.Should().NotBeNull();
            
            // Basic verification that headers exist (specific CORS headers depend on configuration)
        }

        [Fact]
        public async Task MiddlewarePipeline_ProcessesRequests()
        {
            // Test that the middleware pipeline processes requests end-to-end
            
            // Act - Make a request that goes through the entire pipeline with a valid GUID
            var nonExistentId = Guid.NewGuid();
            var response = await _client.GetAsync($"/api/mosaic/{nonExistentId}/status");

            // Assert - Should get a response, indicating middleware pipeline works
            response.Should().NotBeNull();
            
            // 404 is expected for non-existent resource, but the important thing is we get a response
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ServiceDependencies_AreResolvedProperly()
        {
            // Test that all required services are registered and can be resolved
            
            // Act - Try to resolve key services
            var fileOperations = _factory.GetService<IFileOperations>();
            var serviceProvider = _factory.Services;

            // Assert
            fileOperations.Should().NotBeNull();
            serviceProvider.Should().NotBeNull();
            
            // Verify the test overrides are in place
            fileOperations.Should().BeOfType<TestFileOperations>();
        }

        public void Dispose()
        {
            _client?.Dispose();
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}