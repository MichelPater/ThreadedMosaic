using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.BlazorServer.Services
{
    /// <summary>
    /// Service for managing processing state across components
    /// </summary>
    public class ProcessingStateService
    {
        public event Action? StateChanged;

        private readonly Dictionary<string, ProcessingJob> _activeJobs = new();
        private readonly ILogger<ProcessingStateService> _logger;

        public ProcessingStateService(ILogger<ProcessingStateService> logger)
        {
            _logger = logger;
        }

        #region Job Management

        public ProcessingJob CreateJob(string jobId, string jobType, MosaicRequestBase request)
        {
            var job = new ProcessingJob
            {
                Id = jobId,
                Type = jobType,
                Request = request,
                Status = ProcessingStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                Progress = new ProcessingProgress()
            };

            _activeJobs[jobId] = job;
            _logger.LogInformation("Created processing job {JobId} of type {JobType}", jobId, jobType);
            
            NotifyStateChanged();
            return job;
        }

        public ProcessingJob? GetJob(string jobId)
        {
            _activeJobs.TryGetValue(jobId, out var job);
            return job;
        }

        public IEnumerable<ProcessingJob> GetActiveJobs()
        {
            return _activeJobs.Values.Where(j => j.Status == ProcessingStatus.Processing || j.Status == ProcessingStatus.Queued);
        }

        public IEnumerable<ProcessingJob> GetCompletedJobs()
        {
            return _activeJobs.Values.Where(j => j.Status == ProcessingStatus.Completed || j.Status == ProcessingStatus.Failed);
        }

        public bool StartJob(string jobId)
        {
            if (_activeJobs.TryGetValue(jobId, out var job))
            {
                job.Status = ProcessingStatus.Processing;
                job.StartedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Started processing job {JobId}", jobId);
                NotifyStateChanged();
                return true;
            }
            return false;
        }

        public bool CompleteJob(string jobId, string? outputPath = null, string? errorMessage = null)
        {
            if (_activeJobs.TryGetValue(jobId, out var job))
            {
                job.Status = string.IsNullOrEmpty(errorMessage) ? ProcessingStatus.Completed : ProcessingStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.OutputPath = outputPath;
                job.ErrorMessage = errorMessage;
                job.Progress.Percentage = job.Status == ProcessingStatus.Completed ? 100 : job.Progress.Percentage;

                _logger.LogInformation("Completed processing job {JobId} with status {Status}", jobId, job.Status);
                NotifyStateChanged();
                return true;
            }
            return false;
        }

        public bool CancelJob(string jobId)
        {
            if (_activeJobs.TryGetValue(jobId, out var job))
            {
                job.Status = ProcessingStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Cancelled processing job {JobId}", jobId);
                NotifyStateChanged();
                return true;
            }
            return false;
        }

        public void RemoveJob(string jobId)
        {
            if (_activeJobs.Remove(jobId))
            {
                _logger.LogInformation("Removed processing job {JobId}", jobId);
                NotifyStateChanged();
            }
        }

        #endregion

        #region Progress Updates

        public bool UpdateProgress(string jobId, int percentage, string? statusMessage = null, int? processedItems = null, int? totalItems = null)
        {
            if (_activeJobs.TryGetValue(jobId, out var job))
            {
                job.Progress.Percentage = Math.Clamp(percentage, 0, 100);
                if (!string.IsNullOrEmpty(statusMessage))
                    job.Progress.StatusMessage = statusMessage;
                if (processedItems.HasValue)
                    job.Progress.ProcessedItems = processedItems.Value;
                if (totalItems.HasValue)
                    job.Progress.TotalItems = totalItems.Value;

                job.LastUpdated = DateTime.UtcNow;

                _logger.LogDebug("Updated progress for job {JobId}: {Percentage}% - {StatusMessage}", 
                    jobId, percentage, statusMessage);
                
                NotifyStateChanged();
                return true;
            }
            return false;
        }

        public TimeSpan? GetEstimatedTimeRemaining(string jobId)
        {
            if (_activeJobs.TryGetValue(jobId, out var job) && job.StartedAt.HasValue)
            {
                var elapsed = DateTime.UtcNow - job.StartedAt.Value;
                if (job.Progress.Percentage > 0)
                {
                    var totalEstimated = elapsed * (100.0 / job.Progress.Percentage);
                    var remaining = totalEstimated - elapsed;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
            }
            return null;
        }

        #endregion

        #region Cleanup

        public void CleanupCompletedJobs(TimeSpan olderThan)
        {
            var cutoffTime = DateTime.UtcNow - olderThan;
            var jobsToRemove = _activeJobs.Values
                .Where(j => j.CompletedAt.HasValue && j.CompletedAt < cutoffTime)
                .Select(j => j.Id)
                .ToList();

            foreach (var jobId in jobsToRemove)
            {
                RemoveJob(jobId);
            }

            if (jobsToRemove.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} completed jobs", jobsToRemove.Count);
            }
        }

        #endregion

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
    }

    #region Models

    public class ProcessingJob
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public MosaicRequestBase Request { get; set; } = null!;
        public ProcessingStatus Status { get; set; }
        public ProcessingProgress Progress { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? OutputPath { get; set; }
        public string? ErrorMessage { get; set; }

        public TimeSpan? ElapsedTime => StartedAt.HasValue ? 
            (CompletedAt ?? DateTime.UtcNow) - StartedAt.Value : null;
    }

    public class ProcessingProgress
    {
        public int Percentage { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public int ProcessedItems { get; set; }
        public int TotalItems { get; set; }
    }

    public enum ProcessingStatus
    {
        Queued,
        Processing,
        Completed,
        Failed,
        Cancelled
    }

    #endregion
}