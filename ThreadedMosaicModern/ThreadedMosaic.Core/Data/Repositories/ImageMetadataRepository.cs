using Microsoft.EntityFrameworkCore;
using ThreadedMosaic.Core.Data.Models;

namespace ThreadedMosaic.Core.Data.Repositories
{
    /// <summary>
    /// Repository implementation for ImageMetadata operations
    /// </summary>
    public class ImageMetadataRepository : IImageMetadataRepository
    {
        private readonly ThreadedMosaicDbContext _context;

        public ImageMetadataRepository(ThreadedMosaicDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<ImageMetadata?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata.FindAsync([id], cancellationToken);
        }

        public async Task<ImageMetadata?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata
                .FirstOrDefaultAsync(img => img.FilePath == filePath, cancellationToken);
        }

        public async Task<ImageMetadata?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata
                .FirstOrDefaultAsync(img => img.FileHash == fileHash, cancellationToken);
        }

        public async Task<IEnumerable<ImageMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata
                .OrderByDescending(img => img.LastAccessedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<ImageMetadata>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata
                .OrderByDescending(img => img.LastAccessedAt)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<ImageMetadata>> GetByDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata
                .Where(img => img.FilePath.StartsWith(directoryPath))
                .OrderBy(img => img.FileName)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<ImageMetadata>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata
                .Where(img => img.FileName.Contains(searchTerm) || 
                             img.FilePath.Contains(searchTerm) ||
                             (img.Notes != null && img.Notes.Contains(searchTerm)))
                .OrderByDescending(img => img.LastAccessedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<ImageMetadata> AddAsync(ImageMetadata imageMetadata, CancellationToken cancellationToken = default)
        {
            _context.ImageMetadata.Add(imageMetadata);
            await _context.SaveChangesAsync(cancellationToken);
            return imageMetadata;
        }

        public async Task<ImageMetadata> UpdateAsync(ImageMetadata imageMetadata, CancellationToken cancellationToken = default)
        {
            _context.ImageMetadata.Update(imageMetadata);
            await _context.SaveChangesAsync(cancellationToken);
            return imageMetadata;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var imageMetadata = await _context.ImageMetadata.FindAsync([id], cancellationToken);
            if (imageMetadata != null)
            {
                _context.ImageMetadata.Remove(imageMetadata);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task DeleteByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var imageMetadata = await GetByFilePathAsync(filePath, cancellationToken);
            if (imageMetadata != null)
            {
                _context.ImageMetadata.Remove(imageMetadata);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata.AnyAsync(img => img.Id == id, cancellationToken);
        }

        public async Task<bool> ExistsByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata.AnyAsync(img => img.FilePath == filePath, cancellationToken);
        }

        public async Task<bool> ExistsByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata.AnyAsync(img => img.FileHash == fileHash, cancellationToken);
        }

        public async Task<int> CleanupOldEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            var oldEntries = await _context.ImageMetadata
                .Where(img => img.LastAccessedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            _context.ImageMetadata.RemoveRange(oldEntries);
            await _context.SaveChangesAsync(cancellationToken);
            
            return oldEntries.Count;
        }

        public async Task<int> CleanupInvalidEntriesAsync(CancellationToken cancellationToken = default)
        {
            var invalidEntries = await _context.ImageMetadata
                .Where(img => !img.IsValid)
                .ToListAsync(cancellationToken);

            _context.ImageMetadata.RemoveRange(invalidEntries);
            await _context.SaveChangesAsync(cancellationToken);
            
            return invalidEntries.Count;
        }

        public async Task<IEnumerable<ImageMetadata>> GetSimilarColorsAsync(byte red, byte green, byte blue, int tolerance = 20, int maxResults = 50, CancellationToken cancellationToken = default)
        {
            return await _context.ImageMetadata
                .Where(img => 
                    Math.Abs(img.AverageRed - red) <= tolerance &&
                    Math.Abs(img.AverageGreen - green) <= tolerance &&
                    Math.Abs(img.AverageBlue - blue) <= tolerance)
                .OrderBy(img => 
                    Math.Abs(img.AverageRed - red) + 
                    Math.Abs(img.AverageGreen - green) + 
                    Math.Abs(img.AverageBlue - blue))
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<ImageMetadata>> GetSimilarHuesAsync(double hue, double tolerance = 30.0, int maxResults = 50, CancellationToken cancellationToken = default)
        {
            // Handle circular hue values (0-360 degrees)
            var lowerBound = hue - tolerance;
            var upperBound = hue + tolerance;

            IQueryable<ImageMetadata> query;

            if (lowerBound < 0 || upperBound > 360)
            {
                // Handle wrap-around cases
                if (lowerBound < 0)
                {
                    lowerBound += 360;
                    query = _context.ImageMetadata
                        .Where(img => img.AverageHue >= lowerBound || img.AverageHue <= upperBound);
                }
                else // upperBound > 360
                {
                    upperBound -= 360;
                    query = _context.ImageMetadata
                        .Where(img => img.AverageHue >= lowerBound || img.AverageHue <= upperBound);
                }
            }
            else
            {
                // Normal case
                query = _context.ImageMetadata
                    .Where(img => img.AverageHue >= lowerBound && img.AverageHue <= upperBound);
            }

            return await query
                .OrderByDescending(img => img.LastAccessedAt)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }
    }
}