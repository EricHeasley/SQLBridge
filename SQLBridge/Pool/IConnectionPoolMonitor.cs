
namespace SQLBridge {
    public interface IConnectionPoolMonitor {

        /// <summary>
        /// Gets current connection pool statistics.
        /// </summary>
        ConnectionPoolStats GetStats();

        /// <summary>
        /// Clears the ADO.NET connection pool, forcing all connections to be re-established.
        /// Use with caution — typically only needed after failover or credential rotation.
        /// </summary>
        void ClearPool();

    }
}
