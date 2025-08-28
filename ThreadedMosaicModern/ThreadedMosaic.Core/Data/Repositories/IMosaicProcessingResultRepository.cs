using ThreadedMosaic.Core.Data.Models;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Data.Repositories
{
    /// <summary>
    /// Repository interface for MosaicProcessingResult operations
    /// </summary>
    public interface IMosaicProcessingResultRepository
    {
        Task<MosaicProcessingResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<MosaicProcessingResult?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetByStatusAsync(MosaicStatus status, CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetByMosaicTypeAsync(string mosaicType, CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetByMasterImageAsync(Guid masterImageId, CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetCompletedAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetFailedAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<MosaicProcessingResult>> GetInProgressAsync(CancellationToken cancellationToken = default);
        
        Task<MosaicProcessingResult> AddAsync(MosaicProcessingResult mosaicResult, CancellationToken cancellationToken = default);
        Task<MosaicProcessingResult> UpdateAsync(MosaicProcessingResult mosaicResult, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
        
        Task<int> CleanupOldEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
        Task<int> CleanupFailedEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
        
        Task<Dictionary<string, int>> GetMosaicTypeStatisticsAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<MosaicStatus, int>> GetStatusStatisticsAsync(CancellationToken cancellationToken = default);
        Task<TimeSpan?> GetAverageProcessingTimeAsync(string? mosaicType = null, CancellationToken cancellationToken = default);
        
        Task UpdateStatusAsync(Guid id, MosaicStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
        Task UpdateProgressAsync(Guid id, int tilesProcessed, int totalTiles, CancellationToken cancellationToken = default);
    }
}