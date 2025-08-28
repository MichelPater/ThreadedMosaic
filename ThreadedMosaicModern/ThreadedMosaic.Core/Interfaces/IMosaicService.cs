using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for mosaic creation services
    /// </summary>
    public interface IMosaicService
    {
        /// <summary>
        /// Creates a color-based mosaic
        /// </summary>
        /// <param name="request">Color mosaic creation request</param>
        /// <param name="progressReporter">Progress reporter for UI updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result</returns>
        Task<MosaicResult> CreateColorMosaicAsync(ColorMosaicRequest request, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a hue-based mosaic
        /// </summary>
        /// <param name="request">Hue mosaic creation request</param>
        /// <param name="progressReporter">Progress reporter for UI updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result</returns>
        Task<MosaicResult> CreateHueMosaicAsync(HueMosaicRequest request, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a photo-based mosaic
        /// </summary>
        /// <param name="request">Photo mosaic creation request</param>
        /// <param name="progressReporter">Progress reporter for UI updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result</returns>
        Task<MosaicResult> CreatePhotoMosaicAsync(PhotoMosaicRequest request, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the status of a mosaic creation operation
        /// </summary>
        /// <param name="mosaicId">ID of the mosaic operation</param>
        /// <returns>Current status of the operation</returns>
        Task<MosaicStatus> GetMosaicStatusAsync(Guid mosaicId);

        /// <summary>
        /// Cancels a running mosaic operation
        /// </summary>
        /// <param name="mosaicId">ID of the mosaic operation to cancel</param>
        /// <returns>True if cancellation was successful</returns>
        Task<bool> CancelMosaicAsync(Guid mosaicId);

        /// <summary>
        /// Gets a preview/thumbnail of the mosaic being created
        /// </summary>
        /// <param name="mosaicId">ID of the mosaic operation</param>
        /// <returns>Preview image data</returns>
        Task<byte[]?> GetMosaicPreviewAsync(Guid mosaicId);

        /// <summary>
        /// Validates mosaic creation parameters
        /// </summary>
        /// <param name="request">Mosaic request to validate</param>
        /// <returns>Validation result</returns>
        Task<ValidationResult> ValidateMosaicRequestAsync(object request);
    }
}