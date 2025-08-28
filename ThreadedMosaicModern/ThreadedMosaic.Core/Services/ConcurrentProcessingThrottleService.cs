using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Service for throttling concurrent processing operations to protect system resources
    /// </summary>
    public class ConcurrentProcessingThrottleService
    {
        private readonly ILogger<ConcurrentProcessingThrottleService> _logger;
        private readonly IConfiguration _configuration;
        private readonly MemoryManagementService? _memoryManagementService;
        private readonly SemaphoreSlim _processingTasksSemaphore;
        private readonly SemaphoreSlim _backgroundTasksSemaphore;
        private readonly ConcurrentDictionary<string, TaskInfo> _activeTasks;
        
        private readonly int _maxConcurrentTasks;
        private readonly int _maxBackgroundTasks;
        private readonly TimeSpan _taskTimeout;
        private readonly bool _enableResourceBasedThrottling;
        private readonly bool _enablePriorityProcessing;

        public ConcurrentProcessingThrottleService(
            ILogger<ConcurrentProcessingThrottleService> logger,
            IConfiguration configuration,
            MemoryManagementService? memoryManagementService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _memoryManagementService = memoryManagementService;

            // Load configuration settings
            _maxConcurrentTasks = _configuration.GetValue<int>("MosaicConfiguration:Performance:MaxConcurrentTasks", 0);
            if (_maxConcurrentTasks <= 0)
                _maxConcurrentTasks = Math.Max(1, Environment.ProcessorCount / 2); // Default to half of CPU cores

            _maxBackgroundTasks = _configuration.GetValue<int>("MosaicConfiguration:Performance:BackgroundTaskQueueSize", 10);
            _taskTimeout = TimeSpan.FromMinutes(_configuration.GetValue<int>("MosaicConfiguration:Performance:TaskTimeoutMinutes", 30));
            _enableResourceBasedThrottling = _configuration.GetValue<bool>("MosaicConfiguration:Performance:EnableResourceBasedThrottling", true);
            _enablePriorityProcessing = _configuration.GetValue<bool>("MosaicConfiguration:Performance:EnablePriorityProcessing", true);

            _processingTasksSemaphore = new SemaphoreSlim(_maxConcurrentTasks, _maxConcurrentTasks);
            _backgroundTasksSemaphore = new SemaphoreSlim(_maxBackgroundTasks, _maxBackgroundTasks);
            _activeTasks = new ConcurrentDictionary<string, TaskInfo>();

            _logger.LogInformation("ConcurrentProcessingThrottleService initialized: Max concurrent tasks: {MaxTasks}, Max background tasks: {MaxBackground}",
                _maxConcurrentTasks, _maxBackgroundTasks);
        }

        /// <summary>
        /// Executes a high-priority processing task with throttling
        /// </summary>
        public async Task<T> ExecuteProcessingTaskAsync<T>(
            string taskId,
            Func<CancellationToken, Task<T>> taskFactory,
            ProcessingPriority priority = ProcessingPriority.Normal,
            CancellationToken cancellationToken = default)
        {
            var taskInfo = new TaskInfo(taskId, priority, TaskType.Processing);
            
            try
            {
                // Wait for available slot
                if (!await WaitForProcessingSlotAsync(taskInfo, cancellationToken))
                {
                    throw new InvalidOperationException($"Unable to acquire processing slot for task {taskId}");
                }

                // Register memory usage if service is available
                _memoryManagementService?.RegisterProcessingTaskStart();

                _activeTasks[taskId] = taskInfo;
                _logger.LogDebug("Started processing task: {TaskId} (Priority: {Priority})", taskId, priority);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_taskTimeout);

                var result = await taskFactory(timeoutCts.Token);
                
                _logger.LogDebug("Completed processing task: {TaskId} in {Duration:F2}s", 
                    taskId, taskInfo.ElapsedTime.TotalSeconds);

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Processing task cancelled by user: {TaskId}", taskId);
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Processing task timed out after {Timeout}: {TaskId}", _taskTimeout, taskId);
                throw new TimeoutException($"Task {taskId} timed out after {_taskTimeout}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing processing task: {TaskId}", taskId);
                throw;
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                _memoryManagementService?.RegisterProcessingTaskEnd();
                _processingTasksSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a background task with throttling
        /// </summary>
        public async Task ExecuteBackgroundTaskAsync(
            string taskId,
            Func<CancellationToken, Task> taskFactory,
            CancellationToken cancellationToken = default)
        {
            var taskInfo = new TaskInfo(taskId, ProcessingPriority.Low, TaskType.Background);

            try
            {
                await _backgroundTasksSemaphore.WaitAsync(cancellationToken);
                _activeTasks[taskId] = taskInfo;
                
                _logger.LogTrace("Started background task: {TaskId}", taskId);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_taskTimeout);

                await taskFactory(timeoutCts.Token);
                
                _logger.LogTrace("Completed background task: {TaskId} in {Duration:F2}s", 
                    taskId, taskInfo.ElapsedTime.TotalSeconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogTrace("Background task cancelled: {TaskId}", taskId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error executing background task: {TaskId}", taskId);
                throw;
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                _backgroundTasksSemaphore.Release();
            }
        }

        private async Task<bool> WaitForProcessingSlotAsync(TaskInfo taskInfo, CancellationToken cancellationToken)
        {
            var waitStartTime = DateTime.UtcNow;
            
            // Resource-based throttling check
            if (_enableResourceBasedThrottling && _memoryManagementService != null)
            {
                if (!_memoryManagementService.CanStartNewProcessingTask())
                {
                    _logger.LogWarning("Processing task blocked due to high memory usage: {TaskId}", taskInfo.TaskId);
                    
                    // Wait a bit and retry for high-priority tasks
                    if (taskInfo.Priority == ProcessingPriority.High)
                    {
                        var retryCount = 0;
                        while (!_memoryManagementService.CanStartNewProcessingTask() && retryCount < 10)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                            retryCount++;
                        }
                        
                        if (!_memoryManagementService.CanStartNewProcessingTask())
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // Priority-based waiting
            if (_enablePriorityProcessing && taskInfo.Priority == ProcessingPriority.High)
            {
                // High priority tasks get preferential treatment
                var highPriorityTimeout = TimeSpan.FromSeconds(30);
                if (!await _processingTasksSemaphore.WaitAsync(highPriorityTimeout, cancellationToken))
                {
                    _logger.LogWarning("High priority task could not acquire slot within {Timeout}: {TaskId}", 
                        highPriorityTimeout, taskInfo.TaskId);
                    return false;
                }
            }
            else
            {
                await _processingTasksSemaphore.WaitAsync(cancellationToken);
            }

            var waitDuration = DateTime.UtcNow - waitStartTime;
            if (waitDuration > TimeSpan.FromSeconds(5))
            {
                _logger.LogDebug("Task waited {Duration:F2}s for processing slot: {TaskId}", 
                    waitDuration.TotalSeconds, taskInfo.TaskId);
            }

            return true;
        }

        /// <summary>
        /// Gets current throttling status
        /// </summary>
        public ThrottleStatus GetThrottleStatus()
        {
            return new ThrottleStatus
            {
                MaxConcurrentTasks = _maxConcurrentTasks,
                MaxBackgroundTasks = _maxBackgroundTasks,
                AvailableProcessingSlots = _processingTasksSemaphore.CurrentCount,
                AvailableBackgroundSlots = _backgroundTasksSemaphore.CurrentCount,
                ActiveTasks = _activeTasks.Values.ToList(),
                EnableResourceBasedThrottling = _enableResourceBasedThrottling,
                EnablePriorityProcessing = _enablePriorityProcessing
            };
        }

        /// <summary>
        /// Cancels a specific task if it's running
        /// </summary>
        public bool TryCancelTask(string taskId)
        {
            if (_activeTasks.TryGetValue(taskId, out var taskInfo))
            {
                taskInfo.CancellationTokenSource.Cancel();
                _logger.LogInformation("Cancelled task: {TaskId}", taskId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Cancels all tasks of a specific type
        /// </summary>
        public int CancelTasksByType(TaskType taskType)
        {
            var cancelledCount = 0;
            var tasksToCancel = _activeTasks.Values.Where(t => t.TaskType == taskType).ToList();
            
            foreach (var taskInfo in tasksToCancel)
            {
                taskInfo.CancellationTokenSource.Cancel();
                cancelledCount++;
            }
            
            if (cancelledCount > 0)
            {
                _logger.LogInformation("Cancelled {Count} {TaskType} tasks", cancelledCount, taskType);
            }
            
            return cancelledCount;
        }

        /// <summary>
        /// Adjusts the maximum concurrent tasks dynamically
        /// </summary>
        public void AdjustMaxConcurrentTasks(int newMaxTasks)
        {
            if (newMaxTasks <= 0)
                throw new ArgumentException("Max tasks must be positive", nameof(newMaxTasks));

            var currentMax = _processingTasksSemaphore.CurrentCount + 
                            (_maxConcurrentTasks - _processingTasksSemaphore.CurrentCount);
            
            var difference = newMaxTasks - currentMax;
            
            if (difference > 0)
            {
                _processingTasksSemaphore.Release(difference);
            }
            else if (difference < 0)
            {
                // Note: We can't directly reduce semaphore slots, 
                // but new tasks will be limited by the current count
                _logger.LogInformation("Reduced max concurrent tasks to {NewMax} (will take effect as current tasks complete)", 
                    newMaxTasks);
            }
            
            _logger.LogInformation("Adjusted max concurrent tasks from {OldMax} to {NewMax}", 
                currentMax, newMaxTasks);
        }

        public void Dispose()
        {
            _processingTasksSemaphore?.Dispose();
            _backgroundTasksSemaphore?.Dispose();
            
            foreach (var taskInfo in _activeTasks.Values)
            {
                taskInfo.CancellationTokenSource.Dispose();
            }
            _activeTasks.Clear();
        }
    }

    public enum ProcessingPriority
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    public enum TaskType
    {
        Processing,
        Background
    }

    public class TaskInfo
    {
        public string TaskId { get; }
        public ProcessingPriority Priority { get; }
        public TaskType TaskType { get; }
        public DateTime StartTime { get; }
        public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;
        public CancellationTokenSource CancellationTokenSource { get; }

        public TaskInfo(string taskId, ProcessingPriority priority, TaskType taskType)
        {
            TaskId = taskId;
            Priority = priority;
            TaskType = taskType;
            StartTime = DateTime.UtcNow;
            CancellationTokenSource = new CancellationTokenSource();
        }
    }

    public class ThrottleStatus
    {
        public int MaxConcurrentTasks { get; set; }
        public int MaxBackgroundTasks { get; set; }
        public int AvailableProcessingSlots { get; set; }
        public int AvailableBackgroundSlots { get; set; }
        public List<TaskInfo> ActiveTasks { get; set; } = new();
        public bool EnableResourceBasedThrottling { get; set; }
        public bool EnablePriorityProcessing { get; set; }

        public int ActiveProcessingTasks => ActiveTasks.Count(t => t.TaskType == TaskType.Processing);
        public int ActiveBackgroundTasks => ActiveTasks.Count(t => t.TaskType == TaskType.Background);
        public bool IsProcessingAtCapacity => AvailableProcessingSlots == 0;
        public bool IsBackgroundAtCapacity => AvailableBackgroundSlots == 0;
    }
}