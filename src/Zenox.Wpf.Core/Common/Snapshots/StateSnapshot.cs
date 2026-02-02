using System.Text.Json;

namespace Zenox.Wpf.Core.Common.Snapshots
{
	/// <summary>
	/// Represents a snapshot of application state at a point in time
	/// </summary>
	public class StateSnapshot
	{
		/// <summary>
		/// Unique identifier for this snapshot
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// User-friendly name/description
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// When this snapshot was created
		/// </summary>
		public DateTime Timestamp { get; set; }

		/// <summary>
		/// Application version at snapshot time
		/// </summary>
		public string AppVersion { get; set; }

		/// <summary>
		/// Serialized state data (JSON)
		/// </summary>
		public string StateJson { get; set; }

		/// <summary>
		/// Optional metadata
		/// </summary>
		public string Metadata { get; set; }

		/// <summary>
		/// Snapshot type (Manual, Auto, Crash, etc.)
		/// </summary>
		public SnapshotType Type { get; set; }

		/// <summary>
		/// Size in bytes
		/// </summary>
		public long SizeBytes { get; set; }

		public StateSnapshot()
		{
			Id = Guid.NewGuid().ToString();
			Timestamp = DateTime.UtcNow;
		}

		/// <summary>
		/// Create snapshot from state object
		/// </summary>
		public static StateSnapshot Create<T>(T state, string name, SnapshotType type = SnapshotType.Manual)
		{
			var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			return new StateSnapshot
			{
				Name = name,
				StateJson = json,
				Type = type,
				SizeBytes = json.Length,
				AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"
			};
		}

		/// <summary>
		/// Restore state from snapshot
		/// </summary>
		public T Restore<T>()
		{
			if (string.IsNullOrEmpty(StateJson))
				return default;

			return JsonSerializer.Deserialize<T>(StateJson);
		}
	}

	/// <summary>
	/// Type of snapshot
	/// </summary>
	public enum SnapshotType
	{
		Manual,
		Auto,
		Crash,
		BeforeUpdate,
		Custom
	}
}
