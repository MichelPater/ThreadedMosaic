namespace ThreadedMosaic.Mosaic
{
    /// <summary>
    /// Null implementation of IFileOperations that does nothing.
    /// Used for testing to prevent actual file operations from executing.
    /// </summary>
    public class NullFileOperations : IFileOperations
    {
        /// <summary>
        /// Singleton instance for reuse
        /// </summary>
        public static readonly NullFileOperations Instance = new NullFileOperations();

        private NullFileOperations() { }

        public void OpenImageFile(string imageLocation)
        {
            // Do nothing - prevents files from being opened during tests
        }
    }
}