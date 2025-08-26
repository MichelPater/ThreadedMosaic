namespace ThreadedMosaic.Mosaic
{
    /// <summary>
    /// Null implementation of IProgressReporter that does nothing.
    /// Used for testing and scenarios where progress reporting is not needed.
    /// </summary>
    public class NullProgressReporter : IProgressReporter
    {
        /// <summary>
        /// Singleton instance for reuse
        /// </summary>
        public static readonly NullProgressReporter Instance = new NullProgressReporter();

        private NullProgressReporter() { }

        public void SetMaximum(int maximum)
        {
            // Do nothing
        }

        public void IncrementProgress()
        {
            // Do nothing
        }

        public void UpdateStatus(string status)
        {
            // Do nothing
        }

        public void ReportProgress(int current, int maximum, string status)
        {
            // Do nothing
        }
    }
}