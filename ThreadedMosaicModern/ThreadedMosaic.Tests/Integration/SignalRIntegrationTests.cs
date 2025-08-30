using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ThreadedMosaic.Tests.Infrastructure;

namespace ThreadedMosaic.Tests.Integration
{
    /// <summary>
    /// Integration tests for SignalR Hub functionality
    /// </summary>
    public class SignalRIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>, IAsyncDisposable
    {
        private readonly TestWebApplicationFactory<Program> _factory;
        private readonly HttpClient _httpClient;
        private HubConnection? _hubConnection;

        public SignalRIntegrationTests(TestWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _httpClient = _factory.CreateClient();
        }

        [Fact]
        public async Task ProgressHub_CanEstablishConnection()
        {
            // Arrange
            var hubUrl = $"{_httpClient.BaseAddress}progressHub";
            
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            // Act & Assert - Connection should be establishable
            await FluentActions.Invoking(async () => await _hubConnection.StartAsync())
                               .Should().NotThrowAsync();

            _hubConnection.State.Should().Be(HubConnectionState.Connected);
        }

        [Fact]
        public async Task ProgressHub_CanJoinAndLeaveGroups()
        {
            // Arrange
            await SetupHubConnectionAsync();

            var groupName = "testGroup";

            // Act & Assert - Should be able to join and leave groups
            await FluentActions.Invoking(async () => await _hubConnection!.InvokeAsync("JoinGroup", groupName))
                               .Should().NotThrowAsync();

            await FluentActions.Invoking(async () => await _hubConnection!.InvokeAsync("LeaveGroup", groupName))
                               .Should().NotThrowAsync();
        }

        [Fact]
        public async Task ProgressHub_HandlesConcurrentConnections()
        {
            // Test that multiple connections can be established simultaneously
            
            // Arrange
            var hubUrl = $"{_httpClient.BaseAddress}progressHub";
            var connections = new List<HubConnection>();

            try
            {
                // Act - Create multiple connections
                for (int i = 0; i < 3; i++)
                {
                    var connection = new HubConnectionBuilder()
                        .WithUrl(hubUrl, options =>
                        {
                            options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                        })
                        .Build();
                    
                    await connection.StartAsync();
                    connections.Add(connection);
                }

                // Assert - All connections should be active
                foreach (var connection in connections)
                {
                    connection.State.Should().Be(HubConnectionState.Connected);
                }
            }
            finally
            {
                // Cleanup
                foreach (var connection in connections)
                {
                    await connection.DisposeAsync();
                }
            }
        }

        [Fact]
        public async Task ProgressHub_HandlesDisconnectionGracefully()
        {
            // Arrange
            await SetupHubConnectionAsync();

            // Act - Disconnect
            await _hubConnection!.StopAsync();

            // Assert
            _hubConnection.State.Should().Be(HubConnectionState.Disconnected);
        }

        [Fact]
        public async Task ProgressHub_RejectsInvalidMethodCalls()
        {
            // Arrange
            await SetupHubConnectionAsync();

            // Act & Assert - Should handle invalid method calls gracefully
            await FluentActions.Invoking(async () => 
                await _hubConnection!.InvokeAsync("NonExistentMethod", "param"))
                .Should().ThrowAsync<HubException>();
        }

        [Fact]
        public async Task ProgressHub_GroupOperations_WithValidParameters()
        {
            // Arrange
            await SetupHubConnectionAsync();

            var validGroupNames = new[] { "group1", "group-2", "group_3", "123group" };

            // Act & Assert - Should handle valid group names
            foreach (var groupName in validGroupNames)
            {
                await FluentActions.Invoking(async () => 
                    await _hubConnection!.InvokeAsync("JoinGroup", groupName))
                    .Should().NotThrowAsync();

                await FluentActions.Invoking(async () => 
                    await _hubConnection!.InvokeAsync("LeaveGroup", groupName))
                    .Should().NotThrowAsync();
            }
        }

        [Fact]
        public async Task ProgressHub_GroupOperations_WithEdgeCaseParameters()
        {
            // Test behavior with edge case parameters
            
            // Arrange
            await SetupHubConnectionAsync();

            // Act & Assert - Test empty string (may or may not throw based on SignalR implementation)
            try
            {
                await _hubConnection!.InvokeAsync("JoinGroup", "");
                // If no exception, empty strings are handled gracefully
            }
            catch (HubException)
            {
                // If exception, empty strings are rejected - this is also acceptable behavior
            }

            // Test null parameter - this should throw an exception
            await FluentActions.Invoking(async () => 
                await _hubConnection!.InvokeAsync("JoinGroup", (string?)null))
                .Should().ThrowAsync<HubException>();
        }

        [Fact]
        public async Task ProgressHub_Connection_SurvivesQuickReconnects()
        {
            // Test connection stability with quick reconnect scenarios
            
            // Arrange
            await SetupHubConnectionAsync();

            // Act - Quick disconnect and reconnect
            await _hubConnection!.StopAsync();
            _hubConnection.State.Should().Be(HubConnectionState.Disconnected);

            await _hubConnection.StartAsync();

            // Assert
            _hubConnection.State.Should().Be(HubConnectionState.Connected);
        }

        private async Task SetupHubConnectionAsync()
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                return;

            var hubUrl = $"{_httpClient.BaseAddress}progressHub";
            
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            await _hubConnection.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
            
            _httpClient?.Dispose();
        }
    }
}