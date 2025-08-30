using System.Text;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Tests.Infrastructure
{
    /// <summary>
    /// Test implementation of IFileOperations that uses in-memory operations
    /// </summary>
    public class TestFileOperations : IFileOperations
    {
        private readonly Dictionary<string, byte[]> _files = new();
        private readonly HashSet<string> _directories = new();
        private readonly Dictionary<string, DateTime> _fileTimestamps = new();

        // IFileOperations interface methods
        public Task OpenImageFileAsync(string imageLocation, CancellationToken cancellationToken = default)
        {
            // Simulate opening an image file - in test environment just verify file exists
            if (!_files.ContainsKey(imageLocation))
            {
                throw new FileNotFoundException($"Image file not found: {imageLocation}");
            }
            
            // Simulate successful open operation
            return Task.CompletedTask;
        }

        public Task<bool> IsValidImageFileAsync(string filePath)
        {
            // Simple validation - check if file exists and has image-like extension
            if (!_files.ContainsKey(filePath))
            {
                return Task.FromResult(false);
            }

            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
            
            return Task.FromResult(validExtensions.Contains(extension));
        }

        public Task<long> GetFileSizeAsync(string filePath)
        {
            if (!_files.ContainsKey(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            
            return Task.FromResult((long)_files[filePath].Length);
        }

        public Task CopyFileAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!_files.ContainsKey(sourcePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Report progress during copy simulation
            progress?.Report(0.0);
            
            _files[destinationPath] = _files[sourcePath];
            _fileTimestamps[destinationPath] = DateTime.UtcNow;
            
            var directory = Path.GetDirectoryName(destinationPath);
            if (directory != null)
            {
                _directories.Add(directory);
            }
            
            progress?.Report(100.0);
            
            return Task.CompletedTask;
        }

        public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _files.Remove(filePath);
            _fileTimestamps.Remove(filePath);
            
            return Task.CompletedTask;
        }

        public Task<long> GetAvailableDiskSpaceAsync(string path)
        {
            // Simulate available disk space - return a large value for testing
            return Task.FromResult(1024L * 1024 * 1024 * 10); // 10 GB
        }

        public Task<string> CreateTempDirectoryAsync()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "TestMosaic", Guid.NewGuid().ToString());
            _directories.Add(tempPath);
            return Task.FromResult(tempPath);
        }

        public Task CleanupTempFilesAsync(string tempPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Remove all files and directories under the temp path
            var filesToRemove = _files.Keys.Where(k => k.StartsWith(tempPath)).ToList();
            foreach (var file in filesToRemove)
            {
                _files.Remove(file);
                _fileTimestamps.Remove(file);
            }
            
            var directoriesToRemove = _directories.Where(d => d.StartsWith(tempPath)).ToList();
            foreach (var dir in directoriesToRemove)
            {
                _directories.Remove(dir);
            }
            
            return Task.CompletedTask;
        }

        // Additional helper methods for testing
        public Task<byte[]> ReadAllBytesAsync(string filePath)
        {
            if (!_files.ContainsKey(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            
            return Task.FromResult(_files[filePath]);
        }

        public Task<string> ReadAllTextAsync(string filePath)
        {
            var bytes = ReadAllBytesAsync(filePath).Result;
            return Task.FromResult(Encoding.UTF8.GetString(bytes));
        }

        public Task WriteAllBytesAsync(string filePath, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null)
            {
                _directories.Add(directory);
            }
            
            _files[filePath] = bytes;
            _fileTimestamps[filePath] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task WriteAllTextAsync(string filePath, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return WriteAllBytesAsync(filePath, bytes);
        }

        public Task<bool> FileExistsAsync(string filePath)
        {
            return Task.FromResult(_files.ContainsKey(filePath));
        }

        public Task<bool> DirectoryExistsAsync(string directoryPath)
        {
            return Task.FromResult(_directories.Contains(directoryPath));
        }

        public Task CreateDirectoryAsync(string directoryPath)
        {
            _directories.Add(directoryPath);
            return Task.CompletedTask;
        }

        public Task DeleteDirectoryAsync(string directoryPath, bool recursive = false)
        {
            if (recursive)
            {
                var filesToRemove = _files.Keys.Where(k => k.StartsWith(directoryPath)).ToList();
                foreach (var file in filesToRemove)
                {
                    _files.Remove(file);
                    _fileTimestamps.Remove(file);
                }
            }
            
            _directories.Remove(directoryPath);
            return Task.CompletedTask;
        }

        public Task<string[]> GetFilesAsync(string directoryPath, string searchPattern = "*")
        {
            var files = _files.Keys
                .Where(k => k.StartsWith(directoryPath))
                .Where(k => searchPattern == "*" || k.EndsWith(searchPattern.Replace("*", "")))
                .ToArray();
            
            return Task.FromResult(files);
        }

        public Task<string[]> GetDirectoriesAsync(string directoryPath)
        {
            var directories = _directories
                .Where(d => d.StartsWith(directoryPath) && d != directoryPath)
                .ToArray();
            
            return Task.FromResult(directories);
        }

        public Task<DateTime> GetLastWriteTimeAsync(string filePath)
        {
            if (!_fileTimestamps.ContainsKey(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            
            return Task.FromResult(_fileTimestamps[filePath]);
        }

        public Task MoveFileAsync(string sourceFilePath, string destinationFilePath)
        {
            if (!_files.ContainsKey(sourceFilePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
            }
            
            _files[destinationFilePath] = _files[sourceFilePath];
            _fileTimestamps[destinationFilePath] = _fileTimestamps[sourceFilePath];
            
            _files.Remove(sourceFilePath);
            _fileTimestamps.Remove(sourceFilePath);
            
            var directory = Path.GetDirectoryName(destinationFilePath);
            if (directory != null)
            {
                _directories.Add(directory);
            }
            
            return Task.CompletedTask;
        }

        // Test helper methods
        public void AddTestFile(string filePath, byte[] content)
        {
            _files[filePath] = content;
            _fileTimestamps[filePath] = DateTime.UtcNow;
            
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null)
            {
                _directories.Add(directory);
            }
        }

        public void AddTestFile(string filePath, string content)
        {
            AddTestFile(filePath, Encoding.UTF8.GetBytes(content));
        }

        public void ClearAllFiles()
        {
            _files.Clear();
            _directories.Clear();
            _fileTimestamps.Clear();
        }

        public Dictionary<string, byte[]> GetAllFiles()
        {
            return new Dictionary<string, byte[]>(_files);
        }
    }
}