namespace Zenox.NET.Core.Common.CommandReplay
{
	/// <summary>
	/// Represents a recorded command with parameters
	/// </summary>
	public class RecordedCommand
	{
		/// <summary>
		/// Unique identifier
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Command name/identifier
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// When the command was executed
		/// </summary>
		public DateTime Timestamp { get; set; }

		/// <summary>
		/// Serialized parameters (JSON)
		/// </summary>
		public string ParametersJson { get; set; }

		/// <summary>
		/// Command category
		/// </summary>
		public string Category { get; set; }

		/// <summary>
		/// User who executed the command
		/// </summary>
		public string User { get; set; }

		/// <summary>
		/// Execution duration in milliseconds
		/// </summary>
		public long DurationMs { get; set; }

		/// <summary>
		/// Was the command successful
		/// </summary>
		public bool Success { get; set; }

		/// <summary>
		/// Error message if failed
		/// </summary>
		public string ErrorMessage { get; set; }

		public RecordedCommand()
		{
			Id = Guid.NewGuid().ToString();
			Timestamp = DateTime.UtcNow;
			User = Environment.UserName;
		}
	}
}
