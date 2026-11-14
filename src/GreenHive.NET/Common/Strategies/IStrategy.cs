namespace GreenHive.NET.Core.Common.Strategies
{
	/// <summary>
	/// Base interface for all strategy implementations.
	/// Strategies define pluggable algorithms that can be swapped at runtime.
	/// </summary>
	public interface IStrategy
	{
		/// <summary>
		/// Unique identifier for the strategy
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Human-readable description of what the strategy does
		/// </summary>
		string Description { get; }

		/// <summary>
		/// Strategy priority (higher = preferred when multiple match)
		/// </summary>
		int Priority { get; }
	}

	/// <summary>
	/// Generic strategy interface with typed input/output
	/// </summary>
	/// <typeparam name="TInput">Input parameter type</typeparam>
	/// <typeparam name="TOutput">Result type</typeparam>
	public interface IStrategy<TInput, TOutput> : IStrategy
	{
		/// <summary>
		/// Execute the strategy with given input
		/// </summary>
		TOutput Execute(TInput input);

		/// <summary>
		/// Check if this strategy can handle the given input
		/// </summary>
		bool CanHandle(TInput input);
	}
}
