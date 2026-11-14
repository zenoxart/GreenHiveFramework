using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenHive.NET.Core.Common.Strategies
{
    /// <summary>
    /// MVP Strategy Provider Framework
    /// Manages strategy registration, discovery and selection at runtime.
    /// </summary>
    /// <typeparam name="TInput">Strategy input type</typeparam>
    /// <typeparam name="TOutput">Strategy output type</typeparam>
    public class StrategyProvider<TInput, TOutput>
    {
        private readonly List<IStrategy<TInput, TOutput>> _strategies = new();
        private readonly object _lock = new();

        public string Name { get; }

        public StrategyProvider(string name = "DefaultProvider")
        {
            Name = name;
        }

        /// <summary>
        /// Register a strategy
        /// </summary>
        public void Register(IStrategy<TInput, TOutput> strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            lock (_lock)
            {
                if (_strategies.Any(s => s.Name == strategy.Name))
                    throw new InvalidOperationException($"Strategy '{strategy.Name}' is already registered");

                _strategies.Add(strategy);
            }
        }

        /// <summary>
        /// Unregister a strategy by name
        /// </summary>
        public bool Unregister(string strategyName)
        {
            lock (_lock)
            {
                var strategy = _strategies.FirstOrDefault(s => s.Name == strategyName);
                if (strategy != null)
                {
                    _strategies.Remove(strategy);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Execute using the best matching strategy for the input
        /// </summary>
        public TOutput Execute(TInput input)
        {
            var strategy = SelectStrategy(input);
            if (strategy == null)
                throw new InvalidOperationException($"No strategy found that can handle input of type {typeof(TInput).Name}");

            return strategy.Execute(input);
        }

        /// <summary>
        /// Try to execute, returns false if no strategy matches
        /// </summary>
        public bool TryExecute(TInput input, out TOutput result)
        {
            var strategy = SelectStrategy(input);
            if (strategy == null)
            {
                result = default;
                return false;
            }

            result = strategy.Execute(input);
            return true;
        }

        /// <summary>
        /// Execute using a specific strategy by name
        /// </summary>
        public TOutput ExecuteStrategy(string strategyName, TInput input)
        {
            IStrategy<TInput, TOutput> strategy;
            lock (_lock)
            {
                strategy = _strategies.FirstOrDefault(s => s.Name == strategyName);
            }

            if (strategy == null)
                throw new InvalidOperationException($"Strategy '{strategyName}' not found");

            return strategy.Execute(input);
        }

        /// <summary>
        /// Get all registered strategies
        /// </summary>
        public IReadOnlyList<IStrategy<TInput, TOutput>> GetAllStrategies()
        {
            lock (_lock)
            {
                return _strategies.ToList();
            }
        }

        /// <summary>
        /// Get strategy by name
        /// </summary>
        public IStrategy<TInput, TOutput> GetStrategy(string name)
        {
            lock (_lock)
            {
                return _strategies.FirstOrDefault(s => s.Name == name);
            }
        }

        /// <summary>
        /// Select the best strategy for given input
        /// Strategy with highest priority that can handle the input wins
        /// </summary>
        private IStrategy<TInput, TOutput> SelectStrategy(TInput input)
        {
            lock (_lock)
            {
                return _strategies
                    .Where(s => s.CanHandle(input))
                    .OrderByDescending(s => s.Priority)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Clear all registered strategies
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _strategies.Clear();
            }
        }

        /// <summary>
        /// Get count of registered strategies
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _strategies.Count;
                }
            }
        }
    }
}
