using System;

namespace ThreadedMosaic.Mosaic
{
    /// <summary>
    /// Interface for reporting progress during mosaic generation.
    /// Decouples UI concerns from business logic for better testability.
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// Sets the maximum value for progress tracking
        /// </summary>
        /// <param name="maximum">Maximum progress value</param>
        void SetMaximum(int maximum);

        /// <summary>
        /// Increments the current progress by one step
        /// </summary>
        void IncrementProgress();

        /// <summary>
        /// Updates the status text
        /// </summary>
        /// <param name="status">Current status message</param>
        void UpdateStatus(string status);

        /// <summary>
        /// Reports both progress and status in one call
        /// </summary>
        /// <param name="current">Current progress value</param>
        /// <param name="maximum">Maximum progress value</param>
        /// <param name="status">Status message</param>
        void ReportProgress(int current, int maximum, string status);
    }
}