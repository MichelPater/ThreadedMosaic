using System;

namespace ThreadedMosaic.Mosaic
{
    /// <summary>
    /// Interface for file operations that can be mocked for testing.
    /// Separates file system operations from business logic.
    /// </summary>
    public interface IFileOperations
    {
        /// <summary>
        /// Opens an image file for viewing
        /// </summary>
        /// <param name="imageLocation">Path to the image file</param>
        void OpenImageFile(string imageLocation);
    }
}