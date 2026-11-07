using Microsoft.Data.SqlClient;

namespace SQLBridge.Service.Contracts.Core {
    public interface ISqlConnectionFactory {

        Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default);

    }
}
