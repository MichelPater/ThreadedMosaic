using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Console-based progress reporter for command-line scenarios
    /// </summary>
    public class ConsoleProgressReporter : IProgressReporter
    {
        private readonly ILogger<ConsoleProgressReporter> _logger;
        private int _maximum = 100;
        private int _current = 0;
        private string _lastStatus = string.Empty;
        private readonly object _lock = new();

        public ConsoleProgressReporter(ILogger<ConsoleProgressReporter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SetMaximumAsync(int maximum, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _maximum = maximum;
                _current = 0;
            }
            return Task.CompletedTask;
        }

        public Task IncrementProgressAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _current++;
                var percentage = _maximum > 0 ? (double)_current / _maximum * 100 : 0;
                
                Console.Write($"\r{_lastStatus} [{_current}/{_maximum}] {percentage:F1}%");
                
                if (_current >= _maximum)
                {
                    Console.WriteLine(); // New line when complete
                }
            }
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _lastStatus = status ?? string.Empty;
                _logger.LogInformation("{Status}", status);
            }
            return Task.CompletedTask;
        }

        public Task ReportProgressAsync(int current, int maximum, string status, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _current = current;
                _maximum = maximum;
                _lastStatus = status ?? string.Empty;
                
                var percentage = maximum > 0 ? (double)current / maximum * 100 : 0;
                Console.Write($"\r{status} [{current}/{maximum}] {percentage:F1}%");
                
                if (current >= maximum)
                {
                    Console.WriteLine(); // New line when complete
                }
            }
            return Task.CompletedTask;
        }

        public Task ReportDetailedProgressAsync(double percentage, string currentStep, int totalSteps, int currentStepNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var message = $"Step {currentStepNumber}/{totalSteps}: {currentStep} ({percentage:F1}%)";
                Console.Write($"\r{message}");
                
                if (percentage >= 100.0)
                {
                    Console.WriteLine(); // New line when complete
                }
                
                _logger.LogDebug("{Message}", message);
            }
            return Task.CompletedTask;
        }
    }
}