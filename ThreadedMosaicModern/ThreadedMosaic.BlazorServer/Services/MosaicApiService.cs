using System.Net.Http.Json;
using System.Text.Json;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.BlazorServer.Services
{
    /// <summary>
    /// Service for communicating with the ThreadedMosaic API
    /// </summary>
    public class MosaicApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MosaicApiService> _logger;

        public MosaicApiService(IHttpClientFactory httpClientFactory, ILogger<MosaicApiService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ThreadedMosaicApi");
            _logger = logger;
        }

        #region Mosaic Operations

        public async Task<ApiResult<MosaicResult>> CreateColorMosaicAsync(ColorMosaicRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating color mosaic via API");
                var response = await _httpClient.PostAsJsonAsync("/api/mosaic/color", request, cancellationToken);
                return await ProcessResponse<MosaicResult>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating color mosaic");
                return ApiResult<MosaicResult>.Failure($"Failed to create color mosaic: {ex.Message}");
            }
        }

        public async Task<ApiResult<MosaicResult>> CreateHueMosaicAsync(HueMosaicRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating hue mosaic via API");
                var response = await _httpClient.PostAsJsonAsync("/api/mosaic/hue", request, cancellationToken);
                return await ProcessResponse<MosaicResult>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating hue mosaic");
                return ApiResult<MosaicResult>.Failure($"Failed to create hue mosaic: {ex.Message}");
            }
        }

        public async Task<ApiResult<MosaicResult>> CreatePhotoMosaicAsync(PhotoMosaicRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating photo mosaic via API");
                var response = await _httpClient.PostAsJsonAsync("/api/mosaic/photo", request, cancellationToken);
                return await ProcessResponse<MosaicResult>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating photo mosaic");
                return ApiResult<MosaicResult>.Failure($"Failed to create photo mosaic: {ex.Message}");
            }
        }

        public async Task<ApiResult<object>> GetMosaicStatusAsync(string mosaicId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting mosaic status for ID: {MosaicId}", mosaicId);
                var response = await _httpClient.GetAsync($"/api/mosaic/{mosaicId}/status", cancellationToken);
                return await ProcessResponse<object>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mosaic status for ID: {MosaicId}", mosaicId);
                return ApiResult<object>.Failure($"Failed to get mosaic status: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> CancelMosaicAsync(string mosaicId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Cancelling mosaic with ID: {MosaicId}", mosaicId);
                var response = await _httpClient.PostAsync($"/api/mosaic/{mosaicId}/cancel", null, cancellationToken);
                var result = await ProcessResponse<object>(response);
                return ApiResult<bool>.Success(result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling mosaic ID: {MosaicId}", mosaicId);
                return ApiResult<bool>.Failure($"Failed to cancel mosaic: {ex.Message}");
            }
        }

        #endregion

        #region File Operations

        public async Task<ApiResult<FileUploadResult>> UploadMasterImageAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Uploading master image: {FileName}", fileName);
                
                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(fileStream);
                
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(streamContent, "files", fileName);
                content.Add(new StringContent("master"), "type");

                var response = await _httpClient.PostAsync("/api/files/upload", content, cancellationToken);
                return await ProcessResponse<FileUploadResult>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading master image: {FileName}", fileName);
                return ApiResult<FileUploadResult>.Failure($"Failed to upload master image: {ex.Message}");
            }
        }

        public async Task<ApiResult<bool>> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Deleting file with ID: {FileId}", fileId);
                var response = await _httpClient.DeleteAsync($"/api/files/{fileId}", cancellationToken);
                var result = await ProcessResponse<object>(response);
                return ApiResult<bool>.Success(result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file ID: {FileId}", fileId);
                return ApiResult<bool>.Failure($"Failed to delete file: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private async Task<ApiResult<T>> ProcessResponse<T>(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (typeof(T) == typeof(object))
                    {
                        return ApiResult<T>.Success(default(T)!);
                    }

                    var data = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    return ApiResult<T>.Success(data!);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing API response");
                    return ApiResult<T>.Failure("Invalid response format from API");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API request failed with status {StatusCode}: {Error}", response.StatusCode, errorContent);
                return ApiResult<T>.Failure($"API Error ({response.StatusCode}): {errorContent}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Generic result wrapper for API operations
    /// </summary>
    public class ApiResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public string? ErrorMessage { get; private set; }

        private ApiResult(bool isSuccess, T? data, string? errorMessage)
        {
            IsSuccess = isSuccess;
            Data = data;
            ErrorMessage = errorMessage;
        }

        public static ApiResult<T> Success(T data) => new(true, data, null);
        public static ApiResult<T> Failure(string errorMessage) => new(false, default, errorMessage);
    }

    /// <summary>
    /// File upload result model
    /// </summary>
    public class FileUploadResult
    {
        public List<UploadedFile>? Files { get; set; }
    }

    /// <summary>
    /// Uploaded file model
    /// </summary>
    public class UploadedFile
    {
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
    }
}