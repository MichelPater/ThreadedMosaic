using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for photo-based mosaic creation service
    /// </summary>
    public interface IPhotoMosaicService
    {
        /// <summary>
        /// Creates a photo-based mosaic using actual photos matched to color regions
        /// </summary>
        /// <param name="request">Photo mosaic creation parameters</param>
        /// <param name="progressReporter">Optional progress reporter for updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result</returns>
        Task<MosaicResult> CreatePhotoMosaicAsync(
            PhotoMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default);
    }
}