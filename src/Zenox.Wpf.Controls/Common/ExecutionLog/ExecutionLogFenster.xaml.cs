using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Zenox.NET.Core.Common.BackgroundTasks;
using Zenox.NET.Core.Common.Telemetry;
using Zenox.Wpf.Dto;

namespace Zenox.Wpf.Controls.Common.ExecutionLog
{
	/// <summary>
	/// Interaktionslogik für ExecutionLogFenster.xaml
	/// </summary>
	public partial class ExecutionLogFenster : Window, INotifyPropertyChanged
	{
		public static readonly DependencyProperty ExecutionLogListProperty =
			DependencyProperty.Register(
				nameof(ExecutionLogList),
				typeof(ObservableCollection<CommandRecord>),
				typeof(ExecutionLogFenster),
				new PropertyMetadata(null));

		private readonly DispatcherTimer _refreshTimer;
		private double _cpuUsage;
		private long _memoryUsage;
		private int _threadCount;
		private int _activeTasks;

		public ExecutionLogFenster()
		{
			InitializeComponent();
			DataContext = this;

			// Timer für Live-Updates
			_refreshTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			_refreshTimer.Tick += RefreshTimer_Tick;
			_refreshTimer.Start();

			// Subscribe to Telemetry Events
			TelemetryService.Instance.MetricCollected += OnMetricCollected;
			TelemetryService.Instance.EventLogged += OnEventLogged;
		}

		public ObservableCollection<CommandRecord> ExecutionLogList
		{
			get => (ObservableCollection<CommandRecord>)GetValue(ExecutionLogListProperty);
			set => SetValue(ExecutionLogListProperty, value);
		}

		public double CpuUsage
		{
			get => _cpuUsage;
			set
			{
				_cpuUsage = value;
				OnPropertyChanged();
			}
		}

		public long MemoryUsage
		{
			get => _memoryUsage;
			set
			{
				_memoryUsage = value;
				OnPropertyChanged();
			}
		}

		public int ThreadCount
		{
			get => _threadCount;
			set
			{
				_threadCount = value;
				OnPropertyChanged();
			}
		}

		public int ActiveTasks
		{
			get => _activeTasks;
			set
			{
				_activeTasks = value;
				OnPropertyChanged();
			}
		}

		public ObservableCollection<BackgroundTask> BackgroundTasks { get; } = new();
		public ObservableCollection<TelemetryEvent> TelemetryEvents { get; } = new();

		private void OnMetricCollected(object? sender, PerformanceMetric metric)
		{
			Dispatcher.InvokeAsync(() =>
			{
				CpuUsage = metric.CpuUsagePercent;
				MemoryUsage = metric.MemoryUsageMB;
				ThreadCount = metric.ThreadCount;
				ActiveTasks = metric.ActiveTasks;
			});
		}

		private void OnEventLogged(object? sender, TelemetryEvent telemetryEvent)
		{
			Dispatcher.InvokeAsync(() =>
			{
				TelemetryEvents.Insert(0, telemetryEvent);

				// Nur die letzten 100 Events behalten
				while (TelemetryEvents.Count > 100)
				{
					TelemetryEvents.RemoveAt(TelemetryEvents.Count - 1);
				}
			});
		}

		private void RefreshTimer_Tick(object? sender, EventArgs e)
		{
			// Background Tasks aktualisieren
			BackgroundTasks.Clear();
			foreach (var task in BackgroundTaskScheduler.Instance.AllTasks.TakeLast(20))
			{
				BackgroundTasks.Add(task);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			_refreshTimer.Stop();
			TelemetryService.Instance.MetricCollected -= OnMetricCollected;
			TelemetryService.Instance.EventLogged -= OnEventLogged;
			base.OnClosed(e);
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
