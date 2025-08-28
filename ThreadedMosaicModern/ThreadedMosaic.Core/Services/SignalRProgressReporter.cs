using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// SignalR-based progress reporter for real-time updates to connected clients
    /// This is the interface - actual implementation will be in the Blazor/API projects
    /// </summary>
    public class SignalRProgressReporter : IProgressReporter
    {
        private readonly ISignalRHubClient _hubClient;
        private readonly ILogger<SignalRProgressReporter> _logger;
        private readonly string _connectionId;
        private int _maximum = 100;
        private int _current = 0;

        public SignalRProgressReporter(
            ISignalRHubClient hubClient, 
            string connectionId,
            ILogger<SignalRProgressReporter> logger)
        {
            _hubClient = hubClient ?? throw new ArgumentNullException(nameof(hubClient));
            _connectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SetMaximumAsync(int maximum, CancellationToken cancellationToken = default)
        {
            _maximum = maximum;
            _current = 0;

            await _hubClient.SendProgressUpdateAsync(
                _connectionId,
                new ProgressUpdate
                {
                    Current = _current,
                    Maximum = _maximum,
                    Percentage = 0,
                    Status = "Initializing...",
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Set progress maximum to {Maximum} for connection {ConnectionId}", maximum, _connectionId);
        }

        public async Task IncrementProgressAsync(CancellationToken cancellationToken = default)
        {
            _current++;
            var percentage = _maximum > 0 ? (double)_current / _maximum * 100 : 0;

            await _hubClient.SendProgressUpdateAsync(
                _connectionId,
                new ProgressUpdate
                {
                    Current = _current,
                    Maximum = _maximum,
                    Percentage = percentage,
                    Status = $"Processing... {_current}/{_maximum}",
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task UpdateStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            var percentage = _maximum > 0 ? (double)_current / _maximum * 100 : 0;

            await _hubClient.SendProgressUpdateAsync(
                _connectionId,
                new ProgressUpdate
                {
                    Current = _current,
                    Maximum = _maximum,
                    Percentage = percentage,
                    Status = status ?? string.Empty,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Updated status to '{Status}' for connection {ConnectionId}", status, _connectionId);
        }

        public async Task ReportProgressAsync(int current, int maximum, string status, CancellationToken cancellationToken = default)
        {
            _current = current;
            _maximum = maximum;
            var percentage = maximum > 0 ? (double)current / maximum * 100 : 0;

            await _hubClient.SendProgressUpdateAsync(
                _connectionId,
                new ProgressUpdate
                {
                    Current = current,
                    Maximum = maximum,
                    Percentage = percentage,
                    Status = status ?? string.Empty,
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task ReportDetailedProgressAsync(double percentage, string currentStep, int totalSteps, int currentStepNumber, CancellationToken cancellationToken = default)
        {
            await _hubClient.SendDetailedProgressUpdateAsync(
                _connectionId,
                new DetailedProgressUpdate
                {
                    Percentage = percentage,
                    CurrentStep = currentStep ?? string.Empty,
                    TotalSteps = totalSteps,
                    CurrentStepNumber = currentStepNumber,
                    Status = $"Step {currentStepNumber}/{totalSteps}: {currentStep}",
                    Timestamp = DateTime.UtcNow
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Reported detailed progress: {Percentage}% - {CurrentStep} ({StepNumber}/{TotalSteps}) for connection {ConnectionId}", 
                percentage, currentStep, currentStepNumber, totalSteps, _connectionId);
        }
    }

    /// <summary>
    /// Interface for SignalR hub client operations
    /// Implementations will be provided in the API/Blazor projects
    /// </summary>
    public interface ISignalRHubClient
    {
        Task SendProgressUpdateAsync(string connectionId, ProgressUpdate update, CancellationToken cancellationToken = default);
        Task SendDetailedProgressUpdateAsync(string connectionId, DetailedProgressUpdate update, CancellationToken cancellationToken = default);
        Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default);
        Task SendCompletionAsync(string connectionId, string result, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Progress update data transfer object
    /// </summary>
    public class ProgressUpdate
    {
        public int Current { get; set; }
        public int Maximum { get; set; }
        public double Percentage { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Detailed progress update data transfer object
    /// </summary>
    public class DetailedProgressUpdate
    {
        public double Percentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public int TotalSteps { get; set; }
        public int CurrentStepNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}