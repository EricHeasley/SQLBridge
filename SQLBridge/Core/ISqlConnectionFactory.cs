using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal interface ISqlConnectionFactory {

        Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default);

    }
}
