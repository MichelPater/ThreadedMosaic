using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.Services;
using ThreadedMosaic.Core.Data.Repositories;

namespace ThreadedMosaic.Api.Controllers
{
    /// <summary>
    /// Controller for mosaic creation operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [SwaggerTag("Mosaic operations for creating color, hue, and photo-based mosaics")]
    public class MosaicController : ControllerBase
    {
        private readonly IColorMosaicService _colorMosaicService;
        private readonly IHueMosaicService _hueMosaicService;
        private readonly IPhotoMosaicService _photoMosaicService;
        private readonly IMosaicProcessingResultRepository _mosaicResultRepository;
        private readonly IMosaicCancellationService _cancellationService;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly ILogger<MosaicController> _logger;

        public MosaicController(
            IColorMosaicService colorMosaicService,
            IHueMosaicService hueMosaicService,
            IPhotoMosaicService photoMosaicService,
            IMosaicProcessingResultRepository mosaicResultRepository,
            IMosaicCancellationService cancellationService,
            IImageProcessingService imageProcessingService,
            ILogger<MosaicController> logger)
        {
            _colorMosaicService = colorMosaicService ?? throw new ArgumentNullException(nameof(colorMosaicService));
            _hueMosaicService = hueMosaicService ?? throw new ArgumentNullException(nameof(hueMosaicService));
            _photoMosaicService = photoMosaicService ?? throw new ArgumentNullException(nameof(photoMosaicService));
            _mosaicResultRepository = mosaicResultRepository ?? throw new ArgumentNullException(nameof(mosaicResultRepository));
            _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create a color-based mosaic
        /// </summary>
        /// <param name="request">Color mosaic creation parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result with processing details</returns>
        [HttpPost("color")]
        [SwaggerOperation(Summary = "Create color mosaic", Description = "Creates a mosaic by matching colors directly")]
        [SwaggerResponse(200, "Mosaic created successfully", typeof(MosaicResult))]
        [SwaggerResponse(400, "Invalid request parameters")]
        [SwaggerResponse(500, "Internal server error occurred")]
        public async Task<ActionResult<MosaicResult>> CreateColorMosaic(
            [FromBody, Required] ColorMosaicRequest request,
            CancellationToken cancellationToken)
        {
            var mosaicId = Guid.NewGuid();
            
            try
            {
                _logger.LogInformation("Creating color mosaic {MosaicId} for master image: {MasterImagePath}", mosaicId, request.MasterImagePath);
                
                // Register the operation for cancellation tracking
                var operationCancellationToken = _cancellationService.RegisterOperation(mosaicId);
                
                // Combine the request cancellation token with the operation cancellation token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationCancellationToken);
                
                try
                {
                    var result = await _colorMosaicService.CreateColorMosaicAsync(request, null, linkedCts.Token);
                    result.MosaicId = mosaicId; // Ensure the result has the correct ID
                    
                    _logger.LogInformation("Color mosaic {MosaicId} creation completed with status: {Status}", mosaicId, result.Status);
                    return Ok(result);
                }
                catch (OperationCanceledException) when (operationCancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Color mosaic {MosaicId} creation was cancelled", mosaicId);
                    return Ok(new MosaicResult 
                    { 
                        MosaicId = mosaicId, 
                        Status = MosaicStatus.Cancelled, 
                        ErrorMessage = "Operation was cancelled",
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    });
                }
                finally
                {
                    // Unregister the operation when done
                    _cancellationService.UnregisterOperation(mosaicId);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid color mosaic request parameters for {MosaicId}", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return BadRequest(new { error = ex.Message });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Color mosaic {MosaicId} request was cancelled by client", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return StatusCode(499, new { error = "Client cancelled the request" }); // 499 Client Closed Request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating color mosaic {MosaicId}", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return StatusCode(500, new { error = "An error occurred while creating the mosaic" });
            }
        }

        /// <summary>
        /// Create a hue-based mosaic
        /// </summary>
        /// <param name="request">Hue mosaic creation parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result with processing details</returns>
        [HttpPost("hue")]
        [SwaggerOperation(Summary = "Create hue mosaic", Description = "Creates a mosaic by matching hue values with transparency overlays")]
        [SwaggerResponse(200, "Mosaic created successfully", typeof(MosaicResult))]
        [SwaggerResponse(400, "Invalid request parameters")]
        [SwaggerResponse(500, "Internal server error occurred")]
        public async Task<ActionResult<MosaicResult>> CreateHueMosaic(
            [FromBody, Required] HueMosaicRequest request,
            CancellationToken cancellationToken)
        {
            var mosaicId = Guid.NewGuid();
            
            try
            {
                _logger.LogInformation("Creating hue mosaic {MosaicId} for master image: {MasterImagePath}", mosaicId, request.MasterImagePath);
                
                // Register the operation for cancellation tracking
                var operationCancellationToken = _cancellationService.RegisterOperation(mosaicId);
                
                // Combine the request cancellation token with the operation cancellation token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationCancellationToken);
                
                try
                {
                    var result = await _hueMosaicService.CreateHueMosaicAsync(request, null, linkedCts.Token);
                    result.MosaicId = mosaicId; // Ensure the result has the correct ID
                    
                    _logger.LogInformation("Hue mosaic {MosaicId} creation completed with status: {Status}", mosaicId, result.Status);
                    return Ok(result);
                }
                catch (OperationCanceledException) when (operationCancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Hue mosaic {MosaicId} creation was cancelled", mosaicId);
                    return Ok(new MosaicResult 
                    { 
                        MosaicId = mosaicId, 
                        Status = MosaicStatus.Cancelled, 
                        ErrorMessage = "Operation was cancelled",
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    });
                }
                finally
                {
                    // Unregister the operation when done
                    _cancellationService.UnregisterOperation(mosaicId);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid hue mosaic request parameters for {MosaicId}", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return BadRequest(new { error = ex.Message });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Hue mosaic {MosaicId} request was cancelled by client", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return StatusCode(499, new { error = "Client cancelled the request" }); // 499 Client Closed Request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating hue mosaic {MosaicId}", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return StatusCode(500, new { error = "An error occurred while creating the mosaic" });
            }
        }

        /// <summary>
        /// Create a photo-based mosaic
        /// </summary>
        /// <param name="request">Photo mosaic creation parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic creation result with processing details</returns>
        [HttpPost("photo")]
        [SwaggerOperation(Summary = "Create photo mosaic", Description = "Creates a mosaic using actual photos matched to color regions")]
        [SwaggerResponse(200, "Mosaic created successfully", typeof(MosaicResult))]
        [SwaggerResponse(400, "Invalid request parameters")]
        [SwaggerResponse(500, "Internal server error occurred")]
        public async Task<ActionResult<MosaicResult>> CreatePhotoMosaic(
            [FromBody, Required] PhotoMosaicRequest request,
            CancellationToken cancellationToken)
        {
            var mosaicId = Guid.NewGuid();
            
            try
            {
                _logger.LogInformation("Creating photo mosaic {MosaicId} for master image: {MasterImagePath}", mosaicId, request.MasterImagePath);
                
                // Register the operation for cancellation tracking
                var operationCancellationToken = _cancellationService.RegisterOperation(mosaicId);
                
                // Combine the request cancellation token with the operation cancellation token
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationCancellationToken);
                
                try
                {
                    var result = await _photoMosaicService.CreatePhotoMosaicAsync(request, null, linkedCts.Token);
                    result.MosaicId = mosaicId; // Ensure the result has the correct ID
                    
                    _logger.LogInformation("Photo mosaic {MosaicId} creation completed with status: {Status}", mosaicId, result.Status);
                    return Ok(result);
                }
                catch (OperationCanceledException) when (operationCancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Photo mosaic {MosaicId} creation was cancelled", mosaicId);
                    return Ok(new MosaicResult 
                    { 
                        MosaicId = mosaicId, 
                        Status = MosaicStatus.Cancelled, 
                        ErrorMessage = "Operation was cancelled",
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    });
                }
                finally
                {
                    // Unregister the operation when done
                    _cancellationService.UnregisterOperation(mosaicId);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid photo mosaic request parameters for {MosaicId}", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return BadRequest(new { error = ex.Message });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Photo mosaic {MosaicId} request was cancelled by client", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return StatusCode(499, new { error = "Client cancelled the request" }); // 499 Client Closed Request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating photo mosaic {MosaicId}", mosaicId);
                _cancellationService.UnregisterOperation(mosaicId);
                return StatusCode(500, new { error = "An error occurred while creating the mosaic" });
            }
        }

        /// <summary>
        /// Get the processing status of a mosaic
        /// </summary>
        /// <param name="id">Mosaic ID</param>
        /// <returns>Current processing status</returns>
        [HttpGet("{id}/status")]
        [SwaggerOperation(Summary = "Get mosaic status", Description = "Gets the current processing status of a mosaic")]
        [SwaggerResponse(200, "Status retrieved successfully", typeof(object))]
        [SwaggerResponse(404, "Mosaic not found")]
        public async Task<ActionResult> GetMosaicStatus(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting status for mosaic: {MosaicId}", id);
                
                var mosaicResult = await _mosaicResultRepository.GetByIdAsync(id, cancellationToken);
                
                if (mosaicResult == null)
                {
                    _logger.LogWarning("Mosaic not found: {MosaicId}", id);
                    return NotFound(new { error = "Mosaic not found", id });
                }
                
                var status = new
                {
                    id = mosaicResult.Id,
                    status = mosaicResult.Status,
                    mosaicType = mosaicResult.MosaicType,
                    createdAt = mosaicResult.CreatedAt,
                    startedAt = mosaicResult.StartedAt,
                    completedAt = mosaicResult.CompletedAt,
                    processingTime = mosaicResult.ProcessingTime,
                    outputPath = mosaicResult.OutputPath,
                    errorMessage = mosaicResult.ErrorMessage,
                    tilesProcessed = mosaicResult.TilesProcessed,
                    totalTiles = mosaicResult.TotalTiles,
                    processingPercentage = mosaicResult.ProcessingPercentage
                };
                
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mosaic status for ID: {MosaicId}", id);
                return StatusCode(500, new { error = "An error occurred while getting mosaic status" });
            }
        }

        /// <summary>
        /// Cancel a processing mosaic operation
        /// </summary>
        /// <param name="id">Mosaic ID</param>
        /// <param name="cancellationToken">Request cancellation token</param>
        /// <returns>Cancellation result</returns>
        [HttpPost("{id}/cancel")]
        [SwaggerOperation(Summary = "Cancel mosaic", Description = "Cancels a processing mosaic operation")]
        [SwaggerResponse(200, "Mosaic cancelled successfully")]
        [SwaggerResponse(404, "Mosaic not found")]
        [SwaggerResponse(400, "Mosaic cannot be cancelled")]
        public async Task<ActionResult> CancelMosaic(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Cancelling mosaic: {MosaicId}", id);
                
                // Check if the mosaic exists in the database
                var mosaicResult = await _mosaicResultRepository.GetByIdAsync(id, cancellationToken);
                if (mosaicResult == null)
                {
                    _logger.LogWarning("Cannot cancel mosaic {MosaicId} - not found", id);
                    return NotFound(new { id, error = "Mosaic not found" });
                }
                
                // Check if the operation can be cancelled
                if (mosaicResult.Status == MosaicStatus.Completed)
                {
                    _logger.LogInformation("Mosaic {MosaicId} is already completed, cannot cancel", id);
                    return BadRequest(new { id, error = "Mosaic is already completed" });
                }
                
                if (mosaicResult.Status == MosaicStatus.Cancelled)
                {
                    _logger.LogInformation("Mosaic {MosaicId} is already cancelled", id);
                    return Ok(new { id, cancelled = true, message = "Mosaic was already cancelled" });
                }
                
                if (mosaicResult.Status == MosaicStatus.Failed)
                {
                    _logger.LogInformation("Mosaic {MosaicId} has already failed, cannot cancel", id);
                    return BadRequest(new { id, error = "Mosaic has already failed" });
                }
                
                // Attempt to cancel the active operation
                bool wasCancelled = _cancellationService.CancelOperation(id);
                
                if (wasCancelled)
                {
                    // Update the database status to cancelled
                    mosaicResult.Status = MosaicStatus.Cancelled;
                    mosaicResult.CompletedAt = DateTime.UtcNow;
                    mosaicResult.ErrorMessage = "Operation cancelled by user request";
                    
                    await _mosaicResultRepository.UpdateAsync(mosaicResult, cancellationToken);
                    
                    _logger.LogInformation("Successfully cancelled mosaic: {MosaicId}", id);
                    return Ok(new { id, cancelled = true, message = "Mosaic operation cancelled successfully" });
                }
                else
                {
                    // Operation might have already completed or wasn't being tracked
                    _logger.LogWarning("Could not cancel mosaic {MosaicId} - operation not found in cancellation service", id);
                    
                    // Still update the status in case the operation completed between checks
                    if (mosaicResult.Status != MosaicStatus.Completed && mosaicResult.Status != MosaicStatus.Failed)
                    {
                        mosaicResult.Status = MosaicStatus.Cancelled;
                        mosaicResult.CompletedAt = DateTime.UtcNow;
                        mosaicResult.ErrorMessage = "Operation cancelled (was not actively running)";
                        await _mosaicResultRepository.UpdateAsync(mosaicResult, cancellationToken);
                    }
                    
                    return Ok(new { id, cancelled = true, message = "Mosaic marked as cancelled (operation may have already completed)" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling mosaic for ID: {MosaicId}", id);
                return StatusCode(500, new { error = "An error occurred while cancelling the mosaic" });
            }
        }

        /// <summary>
        /// Get a preview/thumbnail of the processing mosaic
        /// </summary>
        /// <param name="id">Mosaic ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Preview image bytes</returns>
        [HttpGet("{id}/preview")]
        [SwaggerOperation(Summary = "Get mosaic preview", Description = "Gets a preview/thumbnail of the processing mosaic")]
        [SwaggerResponse(200, "Preview retrieved successfully", typeof(byte[]))]
        [SwaggerResponse(404, "Mosaic or preview not found")]
        [SwaggerResponse(400, "Mosaic not completed yet")]
        public async Task<ActionResult> GetMosaicPreview(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting preview for mosaic: {MosaicId}", id);
                
                // Check if the mosaic exists
                var mosaicResult = await _mosaicResultRepository.GetByIdAsync(id, cancellationToken);
                if (mosaicResult == null)
                {
                    _logger.LogWarning("Mosaic not found: {MosaicId}", id);
                    return NotFound(new { id, error = "Mosaic not found" });
                }
                
                // Check if the mosaic has an output file
                if (string.IsNullOrEmpty(mosaicResult.OutputPath) || !System.IO.File.Exists(mosaicResult.OutputPath))
                {
                    if (mosaicResult.Status == MosaicStatus.Completed)
                    {
                        _logger.LogWarning("Mosaic {MosaicId} is completed but output file not found at: {OutputPath}", id, mosaicResult.OutputPath);
                        return NotFound(new { id, error = "Mosaic output file not found" });
                    }
                    else
                    {
                        _logger.LogInformation("Mosaic {MosaicId} is not yet completed (Status: {Status})", id, mosaicResult.Status);
                        return BadRequest(new { id, status = mosaicResult.Status.ToString(), error = "Mosaic is not yet completed" });
                    }
                }
                
                // Generate thumbnail/preview
                const int maxPreviewWidth = 400;
                const int maxPreviewHeight = 300;
                
                var previewBytes = await _imageProcessingService.CreateThumbnailAsync(
                    mosaicResult.OutputPath, 
                    maxPreviewWidth, 
                    maxPreviewHeight, 
                    cancellationToken);
                
                _logger.LogInformation("Generated preview for mosaic {MosaicId} ({Size} bytes)", id, previewBytes.Length);
                
                // Determine content type based on the original file extension
                var contentType = GetImageContentType(mosaicResult.OutputPath);
                
                return File(previewBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mosaic preview for ID: {MosaicId}", id);
                return StatusCode(500, new { error = "An error occurred while getting mosaic preview" });
            }
        }

        /// <summary>
        /// Download the completed mosaic result
        /// </summary>
        /// <param name="id">Mosaic ID</param>
        /// <param name="download">If true, forces download; if false, displays inline</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mosaic image file</returns>
        [HttpGet("{id}/result")]
        [SwaggerOperation(Summary = "Download mosaic result", Description = "Downloads the completed mosaic image")]
        [SwaggerResponse(200, "Mosaic downloaded successfully")]
        [SwaggerResponse(404, "Mosaic not found")]
        [SwaggerResponse(400, "Mosaic not completed")]
        public async Task<ActionResult> GetMosaicResult(Guid id, [FromQuery] bool download = true, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting result for mosaic: {MosaicId} (download: {Download})", id, download);
                
                // Check if the mosaic exists
                var mosaicResult = await _mosaicResultRepository.GetByIdAsync(id, cancellationToken);
                if (mosaicResult == null)
                {
                    _logger.LogWarning("Mosaic not found: {MosaicId}", id);
                    return NotFound(new { id, error = "Mosaic not found" });
                }
                
                // Check if the mosaic is completed
                if (mosaicResult.Status != MosaicStatus.Completed)
                {
                    _logger.LogInformation("Mosaic {MosaicId} is not completed (Status: {Status})", id, mosaicResult.Status);
                    return BadRequest(new { id, status = mosaicResult.Status.ToString(), error = "Mosaic is not completed" });
                }
                
                // Check if the output file exists
                if (string.IsNullOrEmpty(mosaicResult.OutputPath) || !System.IO.File.Exists(mosaicResult.OutputPath))
                {
                    _logger.LogWarning("Mosaic {MosaicId} output file not found at: {OutputPath}", id, mosaicResult.OutputPath);
                    return NotFound(new { id, error = "Mosaic output file not found" });
                }
                
                // Read the file
                var fileBytes = await System.IO.File.ReadAllBytesAsync(mosaicResult.OutputPath, cancellationToken);
                var fileName = Path.GetFileName(mosaicResult.OutputPath);
                var contentType = GetImageContentType(mosaicResult.OutputPath);
                
                _logger.LogInformation("Serving mosaic result {MosaicId}: {FileName} ({Size} bytes)", id, fileName, fileBytes.Length);
                
                // Set appropriate headers
                if (download)
                {
                    // Force download with appropriate filename
                    var downloadFileName = $"mosaic_{id}_{fileName}";
                    Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{downloadFileName}\"");
                }
                else
                {
                    // Display inline (for preview in browser)
                    Response.Headers.Append("Content-Disposition", "inline");
                }
                
                // Add cache headers for performance
                Response.Headers.Append("Cache-Control", "public, max-age=3600");
                Response.Headers.Append("Last-Modified", System.IO.File.GetLastWriteTimeUtc(mosaicResult.OutputPath).ToString("R"));
                
                return File(fileBytes, contentType, download ? $"mosaic_{id}_{fileName}" : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mosaic result for ID: {MosaicId}", id);
                return StatusCode(500, new { error = "An error occurred while getting mosaic result" });
            }
        }
        
        /// <summary>
        /// Gets the appropriate content type for an image file based on its extension
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <returns>MIME content type string</returns>
        private static string GetImageContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "image/jpeg" // Default to JPEG
            };
        }
    }
}