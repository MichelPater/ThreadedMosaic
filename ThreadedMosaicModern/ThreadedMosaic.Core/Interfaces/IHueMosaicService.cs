using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for hue-based mosaic creation service
    /// </summary>
    public interface IHueMosaicService
    {
        /// <summary>
        /// Creates a hue-based mosaic by matching hue values with transparency overlays
        /// </summary>
        /// <param name="request">Hue mosaic creation parameters</param>
        /// <param name="progressReporter">Optional progress reporter for updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result</returns>
        Task<MosaicResult> CreateHueMosaicAsync(
            HueMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default);
    }
}