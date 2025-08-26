using System.Diagnostics;

namespace ThreadedMosaic.Mosaic
{
    /// <summary>
    /// Default implementation of IFileOperations that performs actual file operations.
    /// Used in production to open image files for viewing.
    /// </summary>
    public class DefaultFileOperations : IFileOperations
    {
        /// <summary>
        /// Singleton instance for reuse
        /// </summary>
        public static readonly DefaultFileOperations Instance = new DefaultFileOperations();

        private DefaultFileOperations() { }

        public void OpenImageFile(string imageLocation)
        {
            Process.Start(imageLocation);
        }
    }
}