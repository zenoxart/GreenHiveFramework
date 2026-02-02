using System.IO;
using System.Text.Json;
using Zenox.Wpf.Core.Common.Telemetry;

namespace Zenox.Wpf.Core.Common.Snapshots
{
	/// <summary>
	/// MVP Snapshot System
	/// Manages application state snapshots for undo/redo and recovery
	/// </summary>
	public class SnapshotManager
	{
		private static readonly Lazy<SnapshotManager> _instance = new(() => new SnapshotManager());
		public static SnapshotManager Instance => _instance.Value;

		private readonly string _snapshotsDirectory;
		private readonly List<StateSnapshot> _snapshots = new();
		private readonly object _lock = new();
		private const int MaxSnapshots = 50;

		public event EventHandler<StateSnapshot> SnapshotCreated;
		public event EventHandler<StateSnapshot> SnapshotRestored;
		public event EventHandler<string> SnapshotDeleted;

		private SnapshotManager()
		{
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			_snapshotsDirectory = Path.Combine(appData, "VoestalpineWpf", "Snapshots");
			Directory.CreateDirectory(_snapshotsDirectory);
			LoadSnapshots();
		}

		/// <summary>
		/// Create a snapshot of current state
		/// </summary>
		public StateSnapshot CreateSnapshot<T>(T state, string name, SnapshotType type = SnapshotType.Manual)
		{
			lock (_lock)
			{
				var snapshot = StateSnapshot.Create(state, name, type);

				_snapshots.Add(snapshot);
				SaveSnapshot(snapshot);

				// Cleanup old snapshots
				CleanupOldSnapshots();

				TelemetryService.Instance.LogEvent("Snapshot_Created", "Snapshot", new Dictionary<string, object>
				{
					{ "Type", type.ToString() },
					{ "Name", name },
					{ "SizeBytes", snapshot.SizeBytes }
				});

				SnapshotCreated?.Invoke(this, snapshot);
				return snapshot;
			}
		}

		/// <summary>
		/// Get all snapshots
		/// </summary>
		public IReadOnlyList<StateSnapshot> GetSnapshots()
		{
			lock (_lock)
			{
				return _snapshots.OrderByDescending(s => s.Timestamp).ToList();
			}
		}

		/// <summary>
		/// Get snapshots by type
		/// </summary>
		public IReadOnlyList<StateSnapshot> GetSnapshotsByType(SnapshotType type)
		{
			lock (_lock)
			{
				return _snapshots
					.Where(s => s.Type == type)
					.OrderByDescending(s => s.Timestamp)
					.ToList();
			}
		}

		/// <summary>
		/// Get snapshot by ID
		/// </summary>
		public StateSnapshot GetSnapshot(string id)
		{
			lock (_lock)
			{
				return _snapshots.FirstOrDefault(s => s.Id == id);
			}
		}

		/// <summary>
		/// Get most recent snapshot
		/// </summary>
		public StateSnapshot GetLatestSnapshot()
		{
			lock (_lock)
			{
				return _snapshots.OrderByDescending(s => s.Timestamp).FirstOrDefault();
			}
		}

		/// <summary>
		/// Restore state from snapshot
		/// </summary>
		public T RestoreSnapshot<T>(string snapshotId)
		{
			lock (_lock)
			{
				var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
				if (snapshot == null)
					throw new InvalidOperationException($"Snapshot '{snapshotId}' not found");

				var state = snapshot.Restore<T>();

				TelemetryService.Instance.LogEvent("Snapshot_Restored", "Snapshot", new Dictionary<string, object>
				{
					{ "SnapshotId", snapshotId },
					{ "Name", snapshot.Name }
				});

				SnapshotRestored?.Invoke(this, snapshot);
				return state;
			}
		}

		/// <summary>
		/// Delete snapshot
		/// </summary>
		public bool DeleteSnapshot(string snapshotId)
		{
			lock (_lock)
			{
				var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
				if (snapshot == null)
					return false;

				_snapshots.Remove(snapshot);
				DeleteSnapshotFile(snapshot.Id);

				TelemetryService.Instance.LogEvent("Snapshot_Deleted", "Snapshot", new Dictionary<string, object>
				{
					{ "SnapshotId", snapshotId }
				});

				SnapshotDeleted?.Invoke(this, snapshotId);
				return true;
			}
		}

		/// <summary>
		/// Clear all snapshots
		/// </summary>
		public void ClearAll()
		{
			lock (_lock)
			{
				_snapshots.Clear();

				foreach (var file in Directory.GetFiles(_snapshotsDirectory, "*.json"))
				{
					try { File.Delete(file); } catch { }
				}

				TelemetryService.Instance.LogEvent("Snapshots_Cleared");
			}
		}

		/// <summary>
		/// Get total size of all snapshots
		/// </summary>
		public long GetTotalSize()
		{
			lock (_lock)
			{
				return _snapshots.Sum(s => s.SizeBytes);
			}
		}

		private void SaveSnapshot(StateSnapshot snapshot)
		{
			try
			{
				var filePath = Path.Combine(_snapshotsDirectory, $"{snapshot.Id}.json");
				var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(filePath, json);
			}
			catch (Exception ex)
			{
				TelemetryService.Instance.LogError("Snapshot_SaveError", ex, null);
			}
		}

		private void LoadSnapshots()
		{
			try
			{
				foreach (var file in Directory.GetFiles(_snapshotsDirectory, "*.json"))
				{
					try
					{
						var json = File.ReadAllText(file);
						var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);
						if (snapshot != null)
						{
							_snapshots.Add(snapshot);
						}
					}
					catch
					{
						// Skip corrupted files
					}
				}
			}
			catch (Exception ex)
			{
				TelemetryService.Instance.LogError("Snapshots_LoadError", ex, null);
			}
		}

		private void CleanupOldSnapshots()
		{
			if (_snapshots.Count <= MaxSnapshots)
				return;

			var toRemove = _snapshots
				.Where(s => s.Type == SnapshotType.Auto) // Only auto-cleanup auto snapshots
				.OrderBy(s => s.Timestamp)
				.Take(_snapshots.Count - MaxSnapshots)
				.ToList();

			foreach (var snapshot in toRemove)
			{
				_snapshots.Remove(snapshot);
				DeleteSnapshotFile(snapshot.Id);
			}
		}

		private void DeleteSnapshotFile(string snapshotId)
		{
			try
			{
				var filePath = Path.Combine(_snapshotsDirectory, $"{snapshotId}.json");
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
			}
			catch { }
		}
	}
}
