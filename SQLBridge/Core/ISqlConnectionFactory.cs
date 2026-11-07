using Microsoft.Data.SqlClient;

namespace SQLBridge.Core {
    public interface ISqlConnectionFactory {

        Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default);

    }
}
