using Microsoft.Data.SqlClient;

namespace SQLBridge {
    /// <summary>
    /// Manages the lifetime of a command and, when it owns one, its connection.
    /// </summary>
    internal sealed class CommandScope : IAsyncDisposable {

        private readonly SqlConnection? _ownedConnection;

        public SqlCommand Command { get; }

        public CommandScope(SqlCommand command, SqlConnection? ownedConnection) {
            Command = command;
            _ownedConnection = ownedConnection;
        }

        public async ValueTask DisposeAsync() {
            await Command.DisposeAsync();
            if (_ownedConnection is not null) {
                await _ownedConnection.DisposeAsync();
            }
        }

    }
}
