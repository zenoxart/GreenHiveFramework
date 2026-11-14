using System.Diagnostics;

namespace GreenHive.NET.Core.Common.Telemetry
{
	/// <summary>
	/// Repräsentiert eine Performance-Metrik
	/// </summary>
	public class PerformanceMetric
	{
		public DateTime Timestamp { get; set; }
		public double CpuUsagePercent { get; set; }
		public long MemoryUsageMB { get; set; }
		public long WorkingSetMB { get; set; }
		public int ThreadCount { get; set; }
		public int HandleCount { get; set; }
		public long GcTotalMemoryMB { get; set; }
		public int ActiveTasks { get; set; }
	}

	/// <summary>
	/// Telemetrie-Event für Anwendungsereignisse
	/// </summary>
	public class TelemetryEvent
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public DateTime Timestamp { get; set; } = DateTime.Now;
		public string EventName { get; set; } = string.Empty;
		public string Category { get; set; } = string.Empty;
		public Dictionary<string, object> Properties { get; set; } = new();
		public long DurationMs { get; set; }
	}

	/// <summary>
	/// Telemetrie-System für Performance-Monitoring
	/// </summary>
	public class TelemetryService
	{
		private static TelemetryService? _instance;
		private readonly List<PerformanceMetric> _metricsHistory;
		private readonly List<TelemetryEvent> _events;
		private PerformanceCounter? _cpuCounter;
		private Process? _currentProcess;
		private Timer? _metricsTimer;
		private readonly int _maxHistorySize;
		private PerformanceMetric? _currentMetric;
		private bool _isInitialized;
		private readonly object _initLock = new();

		private TelemetryService()
		{
			_metricsHistory = new List<PerformanceMetric>();
			_events = new List<TelemetryEvent>();
			_maxHistorySize = 1000; // Letzte 1000 Metriken speichern
		}

		/// <summary>
		/// Singleton-Instanz
		/// </summary>
		public static TelemetryService Instance => _instance ??= new TelemetryService();

		/// <summary>
		/// Initialisiert das Performance-Monitoring (muss explizit aufgerufen werden)
		/// </summary>
		public void Initialize()
		{
			if (_isInitialized) return;

			lock (_initLock)
			{
				if (_isInitialized) return;

				try
				{
					_currentProcess = Process.GetCurrentProcess();
				}
				catch
				{
					_currentProcess = null;
				}

				// CPU Counter initialisieren - wrapped in try/catch as it can fail on some systems
				try
				{
					if (OperatingSystem.IsWindows())
					{
						_cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
						_cpuCounter.NextValue(); // Erster Aufruf für Initialisierung
					}
				}
				catch
				{
					_cpuCounter = null;
				}

				// Timer für regelmäßige Metrik-Erfassung - verzögert starten
				_metricsTimer = new Timer(CollectMetrics, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

				_isInitialized = true;
			}
		}

		/// <summary>
		/// Aktuelle Performance-Metrik
		/// </summary>
		public PerformanceMetric? CurrentMetric => _currentMetric;

		/// <summary>
		/// Historische Metriken
		/// </summary>
		public IReadOnlyList<PerformanceMetric> MetricsHistory => _metricsHistory.AsReadOnly();

		/// <summary>
		/// Alle Events
		/// </summary>
		public IReadOnlyList<TelemetryEvent> Events => _events.AsReadOnly();

		/// <summary>
		/// Event wenn neue Metrik verfügbar ist
		/// </summary>
		public event EventHandler<PerformanceMetric>? MetricCollected;

		/// <summary>
		/// Event wenn neues Telemetrie-Event geloggt wurde
		/// </summary>
		public event EventHandler<TelemetryEvent>? EventLogged;

		/// <summary>
		/// Sammelt aktuelle Performance-Metriken
		/// </summary>
		private void CollectMetrics(object? state)
		{
			if (!_isInitialized) return;

			try
			{
				_currentProcess?.Refresh();

				double cpuUsage = 0;
				try
				{
					cpuUsage = _cpuCounter?.NextValue() ?? 0;
				}
				catch
				{
					// Ignore CPU counter errors
				}

				int activeTaskCount = 0;
				try
				{
					activeTaskCount = BackgroundTasks.BackgroundTaskScheduler.Instance.ActiveTaskCount;
				}
				catch
				{
					// Ignore if BackgroundTaskScheduler is not initialized
				}

				var metric = new PerformanceMetric
				{
					Timestamp = DateTime.Now,
					CpuUsagePercent = cpuUsage,
					MemoryUsageMB = _currentProcess?.PrivateMemorySize64 / 1024 / 1024 ?? 0,
					WorkingSetMB = _currentProcess?.WorkingSet64 / 1024 / 1024 ?? 0,
					ThreadCount = _currentProcess?.Threads.Count ?? 0,
					HandleCount = _currentProcess?.HandleCount ?? 0,
					GcTotalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
					ActiveTasks = activeTaskCount
				};

				_currentMetric = metric;
				_metricsHistory.Add(metric);

				// Alte Metriken entfernen
				if (_metricsHistory.Count > _maxHistorySize)
				{
					_metricsHistory.RemoveAt(0);
				}

				MetricCollected?.Invoke(this, metric);
			}
			catch
			{
				// Fehler beim Sammeln von Metriken ignorieren
			}
		}

		/// <summary>
		/// Loggt ein Telemetrie-Event
		/// </summary>
		public void LogEvent(string eventName, string category = "General", Dictionary<string, object>? properties = null)
		{
			var telemetryEvent = new TelemetryEvent
			{
				EventName = eventName,
				Category = category,
				Properties = properties ?? new Dictionary<string, object>()
			};

			_events.Add(telemetryEvent);
			EventLogged?.Invoke(this, telemetryEvent);
		}

		/// <summary>
		/// Logs an error event
		/// </summary>
		public void LogError(string eventName, Exception ex, Dictionary<string, object>? properties = null)
		{
			var props = properties ?? new Dictionary<string, object>();
			props["ErrorMessage"] = ex.Message;
			props["StackTrace"] = ex.StackTrace ?? "";
			props["ExceptionType"] = ex.GetType().Name;

			LogEvent(eventName, "Error", props);
		}

		/// <summary>
		/// Startet einen Performance-Tracker für eine Operation
		/// </summary>
		public IDisposable TrackOperation(string operationName, string category = "Performance")
		{
			return new OperationTracker(this, operationName, category);
		}

		/// <summary>
		/// Gibt Performance-Statistiken zurück
		/// </summary>
		public PerformanceStats GetStats()
		{
			if (!_metricsHistory.Any())
				return new PerformanceStats();

			double uptimeSeconds = 0;
			try
			{
				if (_currentProcess != null)
				{
					uptimeSeconds = (DateTime.Now - _currentProcess.StartTime).TotalSeconds;
				}
			}
			catch
			{
				// Ignore if process info unavailable
			}

			return new PerformanceStats
			{
				AverageCpuPercent = _metricsHistory.Average(m => m.CpuUsagePercent),
				AverageMemoryMB = _metricsHistory.Average(m => m.MemoryUsageMB),
				PeakMemoryMB = _metricsHistory.Max(m => m.MemoryUsageMB),
				TotalEvents = _events.Count,
				UptimeSeconds = uptimeSeconds
			};
		}

		/// <summary>
		/// Löscht alte Events
		/// </summary>
		public void ClearOldEvents(TimeSpan olderThan)
		{
			var cutoffTime = DateTime.Now - olderThan;
			_events.RemoveAll(e => e.Timestamp < cutoffTime);
		}

		private class OperationTracker : IDisposable
		{
			private readonly TelemetryService _service;
			private readonly string _operationName;
			private readonly string _category;
			private readonly Stopwatch _stopwatch;

			public OperationTracker(TelemetryService service, string operationName, string category)
			{
				_service = service;
				_operationName = operationName;
				_category = category;
				_stopwatch = Stopwatch.StartNew();
			}

			public void Dispose()
			{
				_stopwatch.Stop();
				_service.LogEvent(_operationName, _category, new Dictionary<string, object>
				{
					["DurationMs"] = _stopwatch.ElapsedMilliseconds
				});
			}
		}
	}

	/// <summary>
	/// Performance-Statistiken
	/// </summary>
	public class PerformanceStats
	{
		public double AverageCpuPercent { get; set; }
		public double AverageMemoryMB { get; set; }
		public long PeakMemoryMB { get; set; }
		public int TotalEvents { get; set; }
		public double UptimeSeconds { get; set; }
	}
}
