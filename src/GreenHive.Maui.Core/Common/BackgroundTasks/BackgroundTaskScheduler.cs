using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace GreenHive.NET.Core.Common.BackgroundTasks
{
    /// <summary>
    /// Scheduler für Hintergrundaufgaben mit Prioritäts-basierter Ausführung
    /// </summary>
    public class BackgroundTaskScheduler
    {
        private static BackgroundTaskScheduler? _instance;
        private readonly ConcurrentQueue<BackgroundTask> _taskQueue;
        private readonly ObservableCollection<BackgroundTask> _allTasks;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private readonly int _maxConcurrentTasks;
        private int _activeTaskCount;
        private bool _isRunning;

        private BackgroundTaskScheduler(int maxConcurrentTasks = 4)
        {
            _maxConcurrentTasks = maxConcurrentTasks;
            _taskQueue = new ConcurrentQueue<BackgroundTask>();
            _allTasks = new ObservableCollection<BackgroundTask>();
            _semaphore = new SemaphoreSlim(_maxConcurrentTasks);
            _shutdownTokenSource = new CancellationTokenSource();
            _activeTaskCount = 0;
            _isRunning = false;
        }

        /// <summary>
        /// Singleton-Instanz des Schedulers
        /// </summary>
        public static BackgroundTaskScheduler Instance => _instance ??= new BackgroundTaskScheduler();

        /// <summary>
        /// Alle registrierten Tasks (Read-Only)
        /// </summary>
        public IReadOnlyCollection<BackgroundTask> AllTasks => _allTasks;

        /// <summary>
        /// Aktive Tasks
        /// </summary>
        public IEnumerable<BackgroundTask> ActiveTasks => 
            _allTasks.Where(t => t.Status == TaskStatus.Running);

        /// <summary>
        /// Anzahl der aktiven Tasks
        /// </summary>
        public int ActiveTaskCount => _activeTaskCount;

        /// <summary>
        /// Event wenn ein Task abgeschlossen wurde
        /// </summary>
        public event EventHandler<BackgroundTask>? TaskCompleted;

        /// <summary>
        /// Event wenn ein Task fehlschlägt
        /// </summary>
        public event EventHandler<(BackgroundTask Task, Exception Error)>? TaskFailed;

        /// <summary>
        /// Startet den Scheduler
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _ = ProcessTaskQueueAsync();
        }

        /// <summary>
        /// Stoppt den Scheduler
        /// </summary>
        public async Task StopAsync()
        {
            _isRunning = false;
            _shutdownTokenSource.Cancel();
            
            // Warte bis alle aktiven Tasks fertig sind
            while (_activeTaskCount > 0)
            {
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Fügt eine neue Aufgabe hinzu
        /// </summary>
        public Guid ScheduleTask(string name, Func<CancellationToken, Task> action, 
            TaskPriority priority = TaskPriority.Normal, string? description = null)
        {
            var task = new BackgroundTask(name, action, priority)
            {
                Description = description
            };

            _allTasks.Add(task);
            EnqueueTask(task);

            return task.Id;
        }

        /// <summary>
        /// Fügt eine wiederkehrende Aufgabe hinzu
        /// </summary>
        public Guid ScheduleRecurringTask(string name, Func<CancellationToken, Task> action,
            TimeSpan interval, TaskPriority priority = TaskPriority.Normal)
        {
            async Task RecurringAction(CancellationToken ct)
            {
                while (!ct.IsCancellationRequested)
                {
                    await action(ct);
                    await Task.Delay(interval, ct);
                }
            }

            return ScheduleTask($"{name} (Recurring)", RecurringAction, priority);
        }

        /// <summary>
        /// Bricht eine Aufgabe ab
        /// </summary>
        public void CancelTask(Guid taskId)
        {
            var task = _allTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null && task.Status == TaskStatus.Running)
            {
                task.Cancel();
            }
        }

        /// <summary>
        /// Entfernt abgeschlossene Tasks aus der Historie
        /// </summary>
        public void ClearCompletedTasks()
        {
            var completedTasks = _allTasks
                .Where(t => t.Status == TaskStatus.Completed || t.Status == TaskStatus.Failed)
                .ToList();

            foreach (var task in completedTasks)
            {
                _allTasks.Remove(task);
            }
        }

        private void EnqueueTask(BackgroundTask task)
        {
            _taskQueue.Enqueue(task);
        }

        private async Task ProcessTaskQueueAsync()
        {
            while (_isRunning)
            {
                try
                {
                    if (_taskQueue.TryDequeue(out var task))
                    {
                        await _semaphore.WaitAsync(_shutdownTokenSource.Token);
                        Interlocked.Increment(ref _activeTaskCount);

                        _ = ExecuteTaskAsync(task);
                    }
                    else
                    {
                        await Task.Delay(100, _shutdownTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ExecuteTaskAsync(BackgroundTask task)
        {
            var cts = new CancellationTokenSource();

            try
            {
                task.Start();
                await task.Action(cts.Token);
                task.Complete();
                TaskCompleted?.Invoke(this, task);
            }
            catch (Exception ex)
            {
                task.Fail(ex);
                TaskFailed?.Invoke(this, (task, ex));

                // Retry-Logik
                if (task.RetryCount < task.MaxRetries)
                {
                    task.RetryCount++;
                    task.Status = TaskStatus.Pending;
                    EnqueueTask(task);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeTaskCount);
                _semaphore.Release();
            }
        }
    }
}
