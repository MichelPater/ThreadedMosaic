using ThreadedMosaic.Core.Data.Models;

namespace ThreadedMosaic.Core.Data.Repositories
{
    /// <summary>
    /// Repository interface for ImageMetadata operations
    /// </summary>
    public interface IImageMetadataRepository
    {
        Task<ImageMetadata?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ImageMetadata?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default);
        Task<ImageMetadata?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default);
        Task<IEnumerable<ImageMetadata>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ImageMetadata>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
        Task<IEnumerable<ImageMetadata>> GetByDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
        Task<IEnumerable<ImageMetadata>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
        
        Task<ImageMetadata> AddAsync(ImageMetadata imageMetadata, CancellationToken cancellationToken = default);
        Task<ImageMetadata> UpdateAsync(ImageMetadata imageMetadata, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task DeleteByFilePathAsync(string filePath, CancellationToken cancellationToken = default);
        
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ExistsByFilePathAsync(string filePath, CancellationToken cancellationToken = default);
        Task<bool> ExistsByFileHashAsync(string fileHash, CancellationToken cancellationToken = default);
        
        Task<int> CleanupOldEntriesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
        Task<int> CleanupInvalidEntriesAsync(CancellationToken cancellationToken = default);
        
        Task<IEnumerable<ImageMetadata>> GetSimilarColorsAsync(byte red, byte green, byte blue, int tolerance = 20, int maxResults = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<ImageMetadata>> GetSimilarHuesAsync(double hue, double tolerance = 30.0, int maxResults = 50, CancellationToken cancellationToken = default);
    }
}