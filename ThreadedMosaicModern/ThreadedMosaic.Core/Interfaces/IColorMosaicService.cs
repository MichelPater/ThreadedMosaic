using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for color-based mosaic creation service
    /// </summary>
    public interface IColorMosaicService
    {
        /// <summary>
        /// Creates a color-based mosaic by matching colors directly
        /// </summary>
        /// <param name="request">Color mosaic creation parameters</param>
        /// <param name="progressReporter">Optional progress reporter for updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result</returns>
        Task<MosaicResult> CreateColorMosaicAsync(
            ColorMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default);
    }
}