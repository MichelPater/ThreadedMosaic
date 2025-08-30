using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Tests.Infrastructure
{
    /// <summary>
    /// Custom WebApplicationFactory for integration testing with test service overrides
    /// </summary>
    public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Replace file operations with test implementation
                services.RemoveAll<IFileOperations>();
                services.AddScoped<IFileOperations, TestFileOperations>();

                // Replace progress hub context with test implementation if it exists
                // Note: This will be implemented when we add the IProgressHubContext interface
                
                // Configure logging for testing
                services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
                
                // Add any other test-specific service overrides here
            });
            
            builder.UseEnvironment("Testing");
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return Services.GetRequiredService<T>();
        }

        public T? GetService<T>()
        {
            return Services.GetService<T>();
        }
    }
}