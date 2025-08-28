using System;
using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Null object pattern implementation for IProgressReporter
    /// Used when no progress reporting is needed
    /// </summary>
    public class NullProgressReporter : IProgressReporter
    {
        public static readonly NullProgressReporter Instance = new();

        private NullProgressReporter() { }

        public Task SetMaximumAsync(int maximum, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task IncrementProgressAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReportProgressAsync(int current, int maximum, string status, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReportDetailedProgressAsync(double percentage, string currentStep, int totalSteps, int currentStepNumber, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}