using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal static class SqlTransientErrorDetector {

        private static readonly HashSet<int> TransientErrorNumbers =
        [
            -2,     // Timeout
            20,     // Instance does not support encryption
            64,     // Connection was successfully established but then an error occurred
            233,    // Connection closed
            10053,  // Transport-level error (WSAECONNABORTED)
            10054,  // Transport-level error (WSAECONNRESET)
            10060,  // Network or instance-specific error (WSAETIMEDOUT)
            40143,  // Connection could not be initialized
            40197,  // Service error processing request
            40501,  // Service is busy
            40613,  // Database is not currently available
            49918,  // Cannot process request - not enough resources
            49919,  // Cannot process create or update request
            49920,  // Cannot process request due to budget
            4060,   // Cannot open database
            4221,   // Login to read-secondary failed
            11001   // Host not found
        ];

        public static bool IsTransient(SqlException ex) {
            foreach (SqlError error in ex.Errors) {
                if (TransientErrorNumbers.Contains(error.Number)) {
                    return true;
                }
            }

            return false;
        }

        public static bool IsTransient(Exception ex) {
            return ex is SqlException sqlEx && IsTransient(sqlEx)
                || ex is TimeoutException;
        }

    }
}
