using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Api.Controllers
{
    /// <summary>
    /// Controller for file upload and management operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [SwaggerTag("File operations for uploading and managing images")]
    public class FilesController : ControllerBase
    {
        private readonly IFileOperations _fileOperations;
        private readonly ILogger<FilesController> _logger;
        private readonly long _maxFileSize = 50 * 1024 * 1024; // 50MB
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" };

        public FilesController(IFileOperations fileOperations, ILogger<FilesController> logger)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Upload a master image or seed images
        /// </summary>
        /// <param name="files">Image files to upload</param>
        /// <param name="type">Upload type: master or seed</param>
        /// <returns>Upload result with file paths</returns>
        [HttpPost("upload")]
        [SwaggerOperation(Summary = "Upload images", Description = "Uploads master image or seed images for mosaic creation")]
        [SwaggerResponse(200, "Files uploaded successfully")]
        [SwaggerResponse(400, "Invalid files or parameters")]
        [SwaggerResponse(413, "File too large")]
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<ActionResult> UploadFiles(
            [Required] IFormFileCollection files,
            [FromQuery, Required] string type)
        {
            try
            {
                if (!files.Any())
                {
                    return BadRequest(new { error = "No files provided" });
                }

                if (type != "master" && type != "seed")
                {
                    return BadRequest(new { error = "Type must be 'master' or 'seed'" });
                }

                var uploadedFiles = new List<UploadedFile>();
                var uploadDirectory = Path.Combine(Path.GetTempPath(), "ThreadedMosaic", type, Guid.NewGuid().ToString());

                if (!Directory.Exists(uploadDirectory))
                {
                    Directory.CreateDirectory(uploadDirectory);
                }

                _logger.LogInformation("Uploading {FileCount} {Type} files", files.Count, type);

                foreach (var file in files)
                {
                    // Validate file
                    var validationResult = ValidateFile(file);
                    if (!validationResult.IsValid)
                    {
                        return BadRequest(new { error = validationResult.Error, fileName = file.FileName });
                    }

                    // Generate unique filename
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(uploadDirectory, fileName);

                    // Save file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Verify file was saved
                    if (!System.IO.File.Exists(filePath))
                    {
                        _logger.LogError("Failed to save file: {FilePath}", filePath);
                        return StatusCode(500, new { error = "Failed to save file", fileName = file.FileName });
                    }

                    uploadedFiles.Add(new UploadedFile
                    {
                        OriginalName = file.FileName,
                        FileName = fileName,
                        FilePath = filePath,
                        Size = file.Length,
                        ContentType = file.ContentType
                    });

                    _logger.LogDebug("File uploaded successfully: {FilePath}", filePath);
                }

                _logger.LogInformation("Successfully uploaded {FileCount} {Type} files to {Directory}", 
                    files.Count, type, uploadDirectory);

                return Ok(new FileUploadResult
                {
                    Message = $"Successfully uploaded {files.Count} {type} files",
                    UploadDirectory = uploadDirectory,
                    Files = uploadedFiles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files");
                return StatusCode(500, new { error = "An error occurred while uploading files" });
            }
        }

        /// <summary>
        /// Delete uploaded files
        /// </summary>
        /// <param name="id">Directory ID or file path to delete</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("{id}")]
        [SwaggerOperation(Summary = "Delete files", Description = "Deletes uploaded files or directories")]
        [SwaggerResponse(200, "Files deleted successfully")]
        [SwaggerResponse(404, "Files not found")]
        [SwaggerResponse(400, "Invalid request")]
        public Task<ActionResult> DeleteFiles(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Task.FromResult<ActionResult>(BadRequest(new { error = "ID is required" }));
                }

                _logger.LogInformation("Deleting files with ID: {Id}", id);

                // Try to parse as directory path first
                var directoryPath = Path.Combine(Path.GetTempPath(), "ThreadedMosaic");
                var targetPath = Path.Combine(directoryPath, id);

                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                    _logger.LogInformation("Deleted directory: {Path}", targetPath);
                    return Task.FromResult<ActionResult>(Ok(new { message = "Directory deleted successfully", path = targetPath }));
                }

                // Try as individual file
                if (System.IO.File.Exists(id))
                {
                    System.IO.File.Delete(id);
                    _logger.LogInformation("Deleted file: {Path}", id);
                    return Task.FromResult<ActionResult>(Ok(new { message = "File deleted successfully", path = id }));
                }

                _logger.LogWarning("File or directory not found: {Id}", id);
                return Task.FromResult<ActionResult>(NotFound(new { error = "File or directory not found" }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting files with ID: {Id}", id);
                return Task.FromResult<ActionResult>(StatusCode(500, new { error = "An error occurred while deleting files" }));
            }
        }

        /// <summary>
        /// Get information about uploaded files
        /// </summary>
        /// <param name="directory">Directory path</param>
        /// <returns>File information</returns>
        [HttpGet("info")]
        [SwaggerOperation(Summary = "Get file info", Description = "Gets information about uploaded files in a directory")]
        [SwaggerResponse(200, "File information retrieved successfully")]
        [SwaggerResponse(404, "Directory not found")]
        public Task<ActionResult> GetFileInfo([FromQuery, Required] string directory)
        {
            try
            {
                if (string.IsNullOrEmpty(directory))
                {
                    return Task.FromResult<ActionResult>(BadRequest(new { error = "Directory parameter is required" }));
                }

                if (!Directory.Exists(directory))
                {
                    return Task.FromResult<ActionResult>(NotFound(new { error = "Directory not found" }));
                }

                var files = Directory.GetFiles(directory)
                    .Where(f => _allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(filePath => new
                    {
                        fileName = Path.GetFileName(filePath),
                        filePath = filePath,
                        size = new FileInfo(filePath).Length,
                        lastModified = new FileInfo(filePath).LastWriteTime
                    })
                    .ToArray();

                _logger.LogInformation("Retrieved info for {FileCount} files in directory: {Directory}", 
                    files.Length, directory);

                return Task.FromResult<ActionResult>(Ok(new
                {
                    directory,
                    fileCount = files.Length,
                    files
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file info for directory: {Directory}", directory);
                return Task.FromResult<ActionResult>(StatusCode(500, new { error = "An error occurred while getting file information" }));
            }
        }

        /// <summary>
        /// Clean up temporary files older than specified hours
        /// </summary>
        /// <param name="hoursOld">Files older than this many hours will be deleted (default: 24)</param>
        /// <returns>Cleanup result</returns>
        [HttpPost("cleanup")]
        [SwaggerOperation(Summary = "Cleanup temporary files", Description = "Removes temporary files older than specified time")]
        [SwaggerResponse(200, "Cleanup completed successfully")]
        public Task<ActionResult> CleanupTempFiles([FromQuery] int hoursOld = 24)
        {
            try
            {
                _logger.LogInformation("Starting cleanup of temporary files older than {Hours} hours", hoursOld);

                var tempDirectory = Path.Combine(Path.GetTempPath(), "ThreadedMosaic");
                if (!Directory.Exists(tempDirectory))
                {
                    return Task.FromResult<ActionResult>(Ok(new { message = "No temporary files to clean up" }));
                }

                var cutoffTime = DateTime.Now.AddHours(-hoursOld);
                var deletedFiles = 0;
                var deletedDirectories = 0;

                // Clean up old directories
                var directories = Directory.GetDirectories(tempDirectory, "*", SearchOption.AllDirectories);
                foreach (var directory in directories)
                {
                    var dirInfo = new DirectoryInfo(directory);
                    if (dirInfo.LastWriteTime < cutoffTime)
                    {
                        try
                        {
                            Directory.Delete(directory, true);
                            deletedDirectories++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete directory: {Directory}", directory);
                        }
                    }
                }

                // Clean up individual files
                var files = Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffTime)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            deletedFiles++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete file: {File}", file);
                        }
                    }
                }

                _logger.LogInformation("Cleanup completed: {DeletedFiles} files and {DeletedDirectories} directories deleted", 
                    deletedFiles, deletedDirectories);

                return Task.FromResult<ActionResult>(Ok(new
                {
                    message = "Cleanup completed successfully",
                    deletedFiles,
                    deletedDirectories,
                    cutoffTime
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
                return Task.FromResult<ActionResult>(StatusCode(500, new { error = "An error occurred during cleanup" }));
            }
        }

        private (bool IsValid, string? Error) ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return (false, "File is empty");

            if (file.Length > _maxFileSize)
                return (false, $"File size exceeds maximum allowed size of {_maxFileSize / 1024 / 1024}MB");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
                return (false, $"File type not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}");

            return (true, null);
        }
    }
}