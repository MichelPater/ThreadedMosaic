using Microsoft.EntityFrameworkCore;
using ThreadedMosaic.Core.Data.Models;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Data.Repositories
{
    /// <summary>
    /// Repository implementation for MosaicProcessingResult operations
    /// </summary>
    public class MosaicProcessingResultRepository : IMosaicProcessingResultRepository
    {
        private readonly ThreadedMosaicDbContext _context;

        public MosaicProcessingResultRepository(ThreadedMosaicDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<MosaicProcessingResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults.FindAsync([id], cancellationToken);
        }

        public async Task<MosaicProcessingResult?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .Include(mr => mr.MasterImage)
                .Include(mr => mr.SeedImages)
                    .ThenInclude(si => si.SeedImage)
                .Include(mr => mr.ProcessingSteps)
                .FirstOrDefaultAsync(mr => mr.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .Include(mr => mr.MasterImage)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .Include(mr => mr.MasterImage)
                .OrderByDescending(mr => mr.CreatedAt)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetByStatusAsync(MosaicStatus status, CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .Include(mr => mr.MasterImage)
                .Where(mr => mr.Status == status)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetByMosaicTypeAsync(string mosaicType, CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .Include(mr => mr.MasterImage)
                .Where(mr => mr.MosaicType == mosaicType)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetByMasterImageAsync(Guid masterImageId, CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .Include(mr => mr.MasterImage)
                .Where(mr => mr.MasterImageId == masterImageId)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetCompletedAsync(CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync(MosaicStatus.Completed, cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetFailedAsync(CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync(MosaicStatus.Failed, cancellationToken);
        }

        public async Task<IEnumerable<MosaicProcessingResult>> GetInProgressAsync(CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .Include(mr => mr.MasterImage)
                .Where(mr => mr.Status == MosaicStatus.Initializing ||
                           mr.Status == MosaicStatus.LoadingImages ||
                           mr.Status == MosaicStatus.AnalyzingImages ||
                           mr.Status == MosaicStatus.CreatingMosaic ||
                           mr.Status == MosaicStatus.Saving)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<MosaicProcessingResult> AddAsync(MosaicProcessingResult mosaicResult, CancellationToken cancellationToken = default)
        {
            _context.MosaicResults.Add(mosaicResult);
            await _context.SaveChangesAsync(cancellationToken);
            return mosaicResult;
        }

        public async Task<MosaicProcessingResult> UpdateAsync(MosaicProcessingResult mosaicResult, CancellationToken cancellationToken = default)
        {
            _context.MosaicResults.Update(mosaicResult);
            await _context.SaveChangesAsync(cancellationToken);
            return mosaicResult;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var mosaicResult = await _context.MosaicResults.FindAsync([id], cancellationToken);
            if (mosaicResult != null)
            {
                _context.MosaicResults.Remove(mosaicResult);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults.AnyAsync(mr => mr.Id == id, cancellationToken);
        }

        public async Task<int> CleanupOldEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            var oldEntries = await _context.MosaicResults
                .Where(mr => mr.CreatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            _context.MosaicResults.RemoveRange(oldEntries);
            await _context.SaveChangesAsync(cancellationToken);
            
            return oldEntries.Count;
        }

        public async Task<int> CleanupFailedEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            var failedEntries = await _context.MosaicResults
                .Where(mr => mr.Status == MosaicStatus.Failed && mr.CreatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            _context.MosaicResults.RemoveRange(failedEntries);
            await _context.SaveChangesAsync(cancellationToken);
            
            return failedEntries.Count;
        }

        public async Task<Dictionary<string, int>> GetMosaicTypeStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .GroupBy(mr => mr.MosaicType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);
        }

        public async Task<Dictionary<MosaicStatus, int>> GetStatusStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.MosaicResults
                .GroupBy(mr => mr.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        }

        public async Task<TimeSpan?> GetAverageProcessingTimeAsync(string? mosaicType = null, CancellationToken cancellationToken = default)
        {
            var query = _context.MosaicResults
                .Where(mr => mr.ProcessingTime.HasValue);

            if (!string.IsNullOrEmpty(mosaicType))
            {
                query = query.Where(mr => mr.MosaicType == mosaicType);
            }

            var processingTimes = await query
                .Select(mr => mr.ProcessingTime!.Value.Ticks)
                .ToListAsync(cancellationToken);

            if (!processingTimes.Any())
                return null;

            var averageTicks = (long)processingTimes.Average();
            return new TimeSpan(averageTicks);
        }

        public async Task UpdateStatusAsync(Guid id, MosaicStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            var mosaicResult = await _context.MosaicResults.FindAsync([id], cancellationToken);
            if (mosaicResult != null)
            {
                mosaicResult.Status = status;
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    mosaicResult.ErrorMessage = errorMessage;
                }

                // Set timestamps based on status
                switch (status)
                {
                    case MosaicStatus.LoadingImages:
                    case MosaicStatus.AnalyzingImages:
                    case MosaicStatus.CreatingMosaic:
                        if (!mosaicResult.StartedAt.HasValue)
                        {
                            mosaicResult.StartedAt = DateTime.UtcNow;
                        }
                        break;
                    case MosaicStatus.Completed:
                    case MosaicStatus.Failed:
                    case MosaicStatus.Cancelled:
                        mosaicResult.CompletedAt = DateTime.UtcNow;
                        break;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task UpdateProgressAsync(Guid id, int tilesProcessed, int totalTiles, CancellationToken cancellationToken = default)
        {
            var mosaicResult = await _context.MosaicResults.FindAsync([id], cancellationToken);
            if (mosaicResult != null)
            {
                mosaicResult.TilesProcessed = tilesProcessed;
                mosaicResult.TotalTiles = totalTiles;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}