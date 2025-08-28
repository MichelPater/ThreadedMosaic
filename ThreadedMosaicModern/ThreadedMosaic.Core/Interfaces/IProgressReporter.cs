using System;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for reporting progress during mosaic generation.
    /// Modernized version with async support and cancellation token support.
    /// Decouples UI concerns from business logic for better testability.
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// Sets the maximum value for progress tracking
        /// </summary>
        /// <param name="maximum">Maximum progress value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetMaximumAsync(int maximum, CancellationToken cancellationToken = default);

        /// <summary>
        /// Increments the current progress by one step
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task IncrementProgressAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the status text
        /// </summary>
        /// <param name="status">Current status message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UpdateStatusAsync(string status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports both progress and status in one call
        /// </summary>
        /// <param name="current">Current progress value</param>
        /// <param name="maximum">Maximum progress value</param>
        /// <param name="status">Status message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ReportProgressAsync(int current, int maximum, string status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports progress with percentage and detailed information
        /// </summary>
        /// <param name="percentage">Progress percentage (0-100)</param>
        /// <param name="currentStep">Current step description</param>
        /// <param name="totalSteps">Total number of steps</param>
        /// <param name="currentStepNumber">Current step number</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ReportDetailedProgressAsync(double percentage, string currentStep, int totalSteps, int currentStepNumber, CancellationToken cancellationToken = default);
    }
}