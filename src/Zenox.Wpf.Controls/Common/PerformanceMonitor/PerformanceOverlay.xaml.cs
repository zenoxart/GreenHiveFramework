using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Threading;
using Zenox.NET.Core.Common.Telemetry;

namespace Zenox.Wpf.Controls.Common.PerformanceMonitor
{
	/// <summary>
	/// Performance Overlay Control - Zeigt Live-Performance-Metriken
	/// </summary>
	public partial class PerformanceOverlay : UserControl, INotifyPropertyChanged
	{
		private readonly DispatcherTimer _updateTimer;
		private readonly Process _currentProcess;
		private readonly DateTime _startTime;

		private double _cpuUsage;
		private long _memoryUsage;
		private long _gcMemory;
		private int _threadCount;
		private int _activeTasks;
		private string _uptime = "00:00:00";

		public PerformanceOverlay()
		{
			InitializeComponent();
			DataContext = this;

			_currentProcess = Process.GetCurrentProcess();
			_startTime = DateTime.Now;

			_updateTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(500)
			};
			_updateTimer.Tick += UpdateTimer_Tick;
			_updateTimer.Start();

			// Subscribe to TelemetryService
			TelemetryService.Instance.MetricCollected += OnMetricCollected;
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

		public long GcMemory
		{
			get => _gcMemory;
			set
			{
				_gcMemory = value;
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

		public string Uptime
		{
			get => _uptime;
			set
			{
				_uptime = value;
				OnPropertyChanged();
			}
		}

		private void OnMetricCollected(object? sender, PerformanceMetric metric)
		{
			Dispatcher.InvokeAsync(() =>
			{
				CpuUsage = metric.CpuUsagePercent;
				MemoryUsage = metric.MemoryUsageMB;
				GcMemory = metric.GcTotalMemoryMB;
				ThreadCount = metric.ThreadCount;
				ActiveTasks = metric.ActiveTasks;
			});
		}

		private void UpdateTimer_Tick(object? sender, EventArgs e)
		{
			var elapsed = DateTime.Now - _startTime;
			Uptime = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
