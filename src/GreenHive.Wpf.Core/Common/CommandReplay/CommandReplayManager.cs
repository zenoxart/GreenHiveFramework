using System.Diagnostics;
using System.IO;
using System.Text.Json;
using GreenHive.NET.Core.Common.Telemetry;

namespace GreenHive.NET.Core.Common.CommandReplay
{
	/// <summary>
	/// MVP Command Replay System
	/// Records user commands and allows replay for automation/testing/debugging
	/// </summary>
	public class CommandReplayManager
	{
		private static readonly Lazy<CommandReplayManager> _instance = new(() => new CommandReplayManager());
		public static CommandReplayManager Instance => _instance.Value;

		private readonly List<RecordedCommand> _history = new();
		private readonly Dictionary<string, Func<string, Task<bool>>> _commandHandlers = new();
		private readonly object _lock = new();
		private readonly string _historyDirectory;
		private bool _isRecording;
		private const int MaxHistorySize = 1000;

		public event EventHandler<RecordedCommand> CommandRecorded;
		public event EventHandler<RecordedCommand> CommandReplayed;
		public event EventHandler<string> ReplaySequenceStarted;
		public event EventHandler<ReplayCompletedEventArgs> ReplaySequenceCompleted;

		private CommandReplayManager()
		{
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			_historyDirectory = Path.Combine(appData, "Wpf", "CommandHistory");
			Directory.CreateDirectory(_historyDirectory);
			_isRecording = true;
		}

		/// <summary>
		/// Is command recording enabled
		/// </summary>
		public bool IsRecording
		{
			get { lock (_lock) { return _isRecording; } }
			set { lock (_lock) { _isRecording = value; } }
		}

		/// <summary>
		/// Register a command handler for replay
		/// </summary>
		public void RegisterHandler(string commandName, Func<string, Task<bool>> handler)
		{
			lock (_lock)
			{
				_commandHandlers[commandName] = handler;
			}
		}

		/// <summary>
		/// Record a command execution
		/// </summary>
		public RecordedCommand RecordCommand(string name, object parameters = null, string category = null)
		{
			if (!IsRecording)
				return null;

			var command = new RecordedCommand
			{
				Name = name,
				Category = category,
				ParametersJson = parameters != null ? JsonSerializer.Serialize(parameters) : null,
				Success = true
			};

			lock (_lock)
			{
				_history.Add(command);
				CleanupHistory();
			}

			TelemetryService.Instance.LogEvent("Command_Recorded", "CommandReplay", new Dictionary<string, object>
			{
				{ "CommandName", name },
				{ "Category", category ?? "None" }
			});

			CommandRecorded?.Invoke(this, command);
			return command;
		}

		/// <summary>
		/// Record command execution with timing
		/// </summary>
		public async Task<RecordedCommand> RecordCommandAsync(string name, Func<Task> action, object parameters = null, string category = null)
		{
			if (!IsRecording)
			{
				await action();
				return null;
			}

			var command = new RecordedCommand
			{
				Name = name,
				Category = category,
				ParametersJson = parameters != null ? JsonSerializer.Serialize(parameters) : null
			};

			var sw = Stopwatch.StartNew();
			try
			{
				await action();
				command.Success = true;
			}
			catch (Exception ex)
			{
				command.Success = false;
				command.ErrorMessage = ex.Message;
				throw;
			}
			finally
			{
				sw.Stop();
				command.DurationMs = sw.ElapsedMilliseconds;

				lock (_lock)
				{
					_history.Add(command);
					CleanupHistory();
				}

				CommandRecorded?.Invoke(this, command);
			}

			return command;
		}

		/// <summary>
		/// Replay a single command
		/// </summary>
		public async Task<bool> ReplayCommand(RecordedCommand command)
		{
			Func<string, Task<bool>> handler;
			lock (_lock)
			{
				if (!_commandHandlers.TryGetValue(command.Name, out handler))
				{
					TelemetryService.Instance.LogEvent("Command_Replay_NoHandler", "CommandReplay", new Dictionary<string, object>
					{
						{ "CommandName", command.Name }
					});
					return false;
				}
			}

			try
			{
				var result = await handler(command.ParametersJson);

				TelemetryService.Instance.LogEvent("Command_Replayed", "CommandReplay", new Dictionary<string, object>
				{
					{ "CommandName", command.Name },
					{ "Success", result }
				});

				CommandReplayed?.Invoke(this, command);
				return result;
			}
			catch (Exception ex)
			{
				TelemetryService.Instance.LogError("Command_Replay_Error", ex, new Dictionary<string, object>
				{
					{ "CommandName", command.Name }
				});
				return false;
			}
		}

		/// <summary>
		/// Replay a sequence of commands
		/// </summary>
		public async Task<ReplayResult> ReplaySequence(IEnumerable<RecordedCommand> commands)
		{
			var sequenceId = Guid.NewGuid().ToString();
			ReplaySequenceStarted?.Invoke(this, sequenceId);

			var result = new ReplayResult { SequenceId = sequenceId };
			var sw = Stopwatch.StartNew();

			foreach (var command in commands)
			{
				result.TotalCommands++;
				var success = await ReplayCommand(command);

				if (success)
					result.SuccessfulCommands++;
				else
					result.FailedCommands++;
			}

			sw.Stop();
			result.DurationMs = sw.ElapsedMilliseconds;

			ReplaySequenceCompleted?.Invoke(this, new ReplayCompletedEventArgs(result));
			return result;
		}

		/// <summary>
		/// Get command history
		/// </summary>
		public IReadOnlyList<RecordedCommand> GetHistory(int maxCount = 100)
		{
			lock (_lock)
			{
				return _history
					.OrderByDescending(c => c.Timestamp)
					.Take(maxCount)
					.ToList();
			}
		}

		/// <summary>
		/// Get commands by category
		/// </summary>
		public IReadOnlyList<RecordedCommand> GetHistoryByCategory(string category, int maxCount = 100)
		{
			lock (_lock)
			{
				return _history
					.Where(c => c.Category == category)
					.OrderByDescending(c => c.Timestamp)
					.Take(maxCount)
					.ToList();
			}
		}

		/// <summary>
		/// Get commands by time range
		/// </summary>
		public IReadOnlyList<RecordedCommand> GetHistoryByTimeRange(DateTime from, DateTime to)
		{
			lock (_lock)
			{
				return _history
					.Where(c => c.Timestamp >= from && c.Timestamp <= to)
					.OrderByDescending(c => c.Timestamp)
					.ToList();
			}
		}

		/// <summary>
		/// Save command sequence to file
		/// </summary>
		public void SaveSequence(IEnumerable<RecordedCommand> commands, string name)
		{
			try
			{
				var sequence = new CommandSequence
				{
					Name = name,
					Commands = commands.ToList(),
					CreatedAt = DateTime.UtcNow
				};

				var filePath = Path.Combine(_historyDirectory, $"{name}.json");
				var json = JsonSerializer.Serialize(sequence, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(filePath, json);

				TelemetryService.Instance.LogEvent("CommandSequence_Saved", "CommandReplay", new Dictionary<string, object>
				{
					{ "Name", name },
					{ "CommandCount", commands.Count() }
				});
			}
			catch (Exception ex)
			{
				TelemetryService.Instance.LogError("CommandSequence_SaveError", ex, null);
			}
		}

		/// <summary>
		/// Load command sequence from file
		/// </summary>
		public CommandSequence LoadSequence(string name)
		{
			try
			{
				var filePath = Path.Combine(_historyDirectory, $"{name}.json");
				if (!File.Exists(filePath))
					return null;

				var json = File.ReadAllText(filePath);
				return JsonSerializer.Deserialize<CommandSequence>(json);
			}
			catch (Exception ex)
			{
				TelemetryService.Instance.LogError("CommandSequence_LoadError", ex, null);
				return null;
			}
		}

		/// <summary>
		/// Get all saved sequences
		/// </summary>
		public IReadOnlyList<string> GetSavedSequences()
		{
			try
			{
				return Directory.GetFiles(_historyDirectory, "*.json")
					.Select(Path.GetFileNameWithoutExtension)
					.ToList();
			}
			catch
			{
				return new List<string>();
			}
		}

		/// <summary>
		/// Clear history
		/// </summary>
		public void ClearHistory()
		{
			lock (_lock)
			{
				_history.Clear();
			}
			TelemetryService.Instance.LogEvent("CommandHistory_Cleared", "CommandReplay");
		}

		private void CleanupHistory()
		{
			if (_history.Count > MaxHistorySize)
			{
				var toRemove = _history.Count - MaxHistorySize;
				_history.RemoveRange(0, toRemove);
			}
		}
	}

	/// <summary>
	/// Result of a replay sequence
	/// </summary>
	public class ReplayResult
	{
		public string SequenceId { get; set; }
		public int TotalCommands { get; set; }
		public int SuccessfulCommands { get; set; }
		public int FailedCommands { get; set; }
		public long DurationMs { get; set; }

		public bool AllSuccessful => FailedCommands == 0;
		public double SuccessRate => TotalCommands > 0 ? (double)SuccessfulCommands / TotalCommands : 0;
	}

	/// <summary>
	/// Saved command sequence
	/// </summary>
	public class CommandSequence
	{
		public string Name { get; set; }
		public DateTime CreatedAt { get; set; }
		public List<RecordedCommand> Commands { get; set; }
	}

	/// <summary>
	/// Event args for replay completion
	/// </summary>
	public class ReplayCompletedEventArgs : EventArgs
	{
		public ReplayResult Result { get; }

		public ReplayCompletedEventArgs(ReplayResult result)
		{
			Result = result;
		}
	}
}
