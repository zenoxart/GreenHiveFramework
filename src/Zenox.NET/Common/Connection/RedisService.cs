using System;
using StackExchange.Redis;
using System.Threading.Tasks;
using Zenox.NET.Core.Common;
using Zenox.NET.Core.Common.Services;
using Zenox.NET.Core.Common.MVVM.FactoryInjection;

namespace Zenox.NET.Core.Common.Connection
{
    /// <summary>
    /// Stellt Methoden für die Arbeit mit Redis bereit.
    /// </summary>
    public class RedisService : AppObject
    {

        /// <summary>
        /// Gets the connection to the Redis server.
        /// </summary>
        /// <remarks>This property provides access to the underlying connection used for interacting with
        /// the Redis server. The connection is managed internally and is read-only to ensure proper lifecycle
        /// management.</remarks>
        public ConnectionMultiplexer Connection { get; private set; }
        public IDatabase Database { get; private set; }

        /// <summary>
        /// Stellt eine Verbindung zu Redis her.
        /// </summary>
        public void Connect(string connectionString)
        {
            try
            {
                Connection = ConnectionMultiplexer.Connect(connectionString);
                Database = Connection.GetDatabase();
            }
            catch (Exception ex)
            {
                ExceptionHandler.Logger?.LogException(ex, "RedisService.Connect");
                Logger?.LogException(ex, "RedisService.Connect");
            }
        }

        /// <summary>
        /// Setzt einen Wert für einen Schlüssel.
        /// </summary>
        public async Task SetAsync(string key, string value)
        {
            try
            {
                await Database.StringSetAsync(key, value);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Logger?.LogException(ex, "RedisService.SetAsync");
                Logger?.LogException(ex, "RedisService.SetAsync");
            }
        }

        /// <summary>
        /// Liest einen Wert für einen Schlüssel.
        /// </summary>
        public async Task<string> GetAsync(string key)
        {
            try
            {
                return await Database.StringGetAsync(key);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Logger?.LogException(ex, "RedisService.GetAsync");
                Logger?.LogException(ex, "RedisService.GetAsync");
                return null;
            }
        }
    }
}
