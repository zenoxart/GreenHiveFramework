using System.Text.Json;

namespace Zenox.NET.Core.Common.Snapshots
{
	/// <summary>
	/// Represents a snapshot of application state at a point in time
	/// </summary>
	public class StateSnapshot
	{
		/// <summary>
		/// Unique identifier for this snapshot
		/// </summary>
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// User-friendly name/description
		/// </summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// When this snapshot was created
		/// </summary>
		public DateTime Timestamp { get; set; }

		/// <summary>
		/// Application version at snapshot time
		/// </summary>
		public string AppVersion { get; set; } = string.Empty;

		/// <summary>
		/// Serialized state data (JSON)
		/// </summary>
		public string StateJson { get; set; } = string.Empty;

		/// <summary>
		/// Optional metadata
		/// </summary>
		public string Metadata { get; set; } = string.Empty;

		/// <summary>
		/// Snapshot type (Manual, Auto, Crash, etc.)
		/// </summary>
		public SnapshotType Type { get; set; }

		/// <summary>
		/// Snapshot category for organizational purposes
		/// </summary>
		public SnapshotCategory Category { get; set; }

		/// <summary>
		/// Size in bytes
		/// </summary>
		public long SizeBytes { get; set; }

		/// <summary>
		/// Full type name of the stored data for type-safe restoration
		/// </summary>
		public string DataTypeName { get; set; } = string.Empty;

		public StateSnapshot()
		{
			Id = Guid.NewGuid().ToString();
			Timestamp = DateTime.UtcNow;
		}

		/// <summary>
		/// Create snapshot from state object
		/// </summary>
		public static StateSnapshot Create<T>(T state, string name, SnapshotType type = SnapshotType.Manual, SnapshotCategory category = SnapshotCategory.Custom)
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
				Category = category,
				SizeBytes = json.Length,
				DataTypeName = typeof(T).FullName ?? typeof(T).Name,
				AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"
			};
		}

		/// <summary>
		/// Restore state from snapshot
		/// </summary>
		public T? Restore<T>()
		{
			if (string.IsNullOrEmpty(StateJson))
				return default;

			return JsonSerializer.Deserialize<T>(StateJson);
		}

		/// <summary>
		/// Attempt to restore the state as a dynamic object
		/// </summary>
		public object? RestoreDynamic()
		{
			if (string.IsNullOrEmpty(StateJson))
				return null;

			return JsonSerializer.Deserialize<object>(StateJson);
		}
	}

	/// <summary>
	/// Type of snapshot
	/// </summary>
	public enum SnapshotType
	{
		/// <summary>
		/// Manually created by the user
		/// </summary>
		Manual,

		/// <summary>
		/// Automatically created
		/// </summary>
		Auto,

		/// <summary>
		/// Created on crash
		/// </summary>
		Crash,

		/// <summary>
		/// Created before update
		/// </summary>
		BeforeUpdate,

		/// <summary>
		/// Created at application startup
		/// </summary>
		Startup,

		/// <summary>
		/// Created at application shutdown
		/// </summary>
		Shutdown,

		/// <summary>
		/// User-defined
		/// </summary>
		Custom
	}

	/// <summary>
	/// Category of snapshot for organized storage
	/// </summary>
	public enum SnapshotCategory
	{
		/// <summary>
		/// Window layout and position
		/// </summary>
		WindowLayout,

		/// <summary>
		/// User settings
		/// </summary>
		UserSettings,

		/// <summary>
		/// Application configuration
		/// </summary>
		AppConfiguration,

		/// <summary>
		/// Session data
		/// </summary>
		SessionData,

		/// <summary>
		/// Custom user data
		/// </summary>
		Custom
	}
}
