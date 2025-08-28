using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.Services;

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
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MosaicController> _logger;

        public MosaicController(IServiceProvider serviceProvider, ILogger<MosaicController> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
            try
            {
                _logger.LogInformation("Creating color mosaic for master image: {MasterImagePath}", request.MasterImagePath);
                
                var service = _serviceProvider.GetRequiredService<ColorMosaicService>();
                var result = await service.CreateColorMosaicAsync(request, null, cancellationToken);
                
                _logger.LogInformation("Color mosaic creation completed with status: {Status}", result.Status);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid color mosaic request parameters");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating color mosaic");
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
            try
            {
                _logger.LogInformation("Creating hue mosaic for master image: {MasterImagePath}", request.MasterImagePath);
                
                var service = _serviceProvider.GetRequiredService<HueMosaicService>();
                var result = await service.CreateHueMosaicAsync(request, null, cancellationToken);
                
                _logger.LogInformation("Hue mosaic creation completed with status: {Status}", result.Status);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid hue mosaic request parameters");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating hue mosaic");
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
            try
            {
                _logger.LogInformation("Creating photo mosaic for master image: {MasterImagePath}", request.MasterImagePath);
                
                var service = _serviceProvider.GetRequiredService<PhotoMosaicService>();
                var result = await service.CreatePhotoMosaicAsync(request, null, cancellationToken);
                
                _logger.LogInformation("Photo mosaic creation completed with status: {Status}", result.Status);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid photo mosaic request parameters");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating photo mosaic");
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
        public async Task<ActionResult> GetMosaicStatus(Guid id)
        {
            try
            {
                // TODO: Implement status tracking with a status store
                _logger.LogInformation("Getting status for mosaic: {MosaicId}", id);
                return Ok(new { id, status = MosaicStatus.Unknown, message = "Status tracking not yet implemented" });
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
        /// <returns>Cancellation result</returns>
        [HttpPost("{id}/cancel")]
        [SwaggerOperation(Summary = "Cancel mosaic", Description = "Cancels a processing mosaic operation")]
        [SwaggerResponse(200, "Mosaic cancelled successfully")]
        [SwaggerResponse(404, "Mosaic not found")]
        [SwaggerResponse(400, "Mosaic cannot be cancelled")]
        public async Task<ActionResult> CancelMosaic(Guid id)
        {
            try
            {
                // TODO: Implement cancellation tracking
                _logger.LogInformation("Cancelling mosaic: {MosaicId}", id);
                return Ok(new { id, cancelled = false, message = "Cancellation not yet implemented" });
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
        /// <returns>Preview image bytes</returns>
        [HttpGet("{id}/preview")]
        [SwaggerOperation(Summary = "Get mosaic preview", Description = "Gets a preview/thumbnail of the processing mosaic")]
        [SwaggerResponse(200, "Preview retrieved successfully", typeof(byte[]))]
        [SwaggerResponse(404, "Mosaic or preview not found")]
        public async Task<ActionResult> GetMosaicPreview(Guid id)
        {
            try
            {
                // TODO: Implement preview generation
                _logger.LogInformation("Getting preview for mosaic: {MosaicId}", id);
                return NotFound(new { id, message = "Preview generation not yet implemented" });
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
        /// <returns>Mosaic image file</returns>
        [HttpGet("{id}/result")]
        [SwaggerOperation(Summary = "Download mosaic result", Description = "Downloads the completed mosaic image")]
        [SwaggerResponse(200, "Mosaic downloaded successfully")]
        [SwaggerResponse(404, "Mosaic not found")]
        [SwaggerResponse(400, "Mosaic not completed")]
        public async Task<ActionResult> GetMosaicResult(Guid id)
        {
            try
            {
                // TODO: Implement result file serving
                _logger.LogInformation("Getting result for mosaic: {MosaicId}", id);
                return NotFound(new { id, message = "Result serving not yet implemented" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mosaic result for ID: {MosaicId}", id);
                return StatusCode(500, new { error = "An error occurred while getting mosaic result" });
            }
        }
    }
}