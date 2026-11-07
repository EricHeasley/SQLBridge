using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal sealed class SqlTransactionWrapper : SqlOperations, ISqlTransaction {

        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;
        private readonly RetryPolicy _retryPolicy;
        private bool _disposed;

        internal SqlTransactionWrapper(
            SqlConnection connection,
            SqlTransaction transaction,
            RetryPolicy retryPolicy,
            SqlBridgeOptions options) : base(options) {
            _connection = connection;
            _transaction = transaction;
            _retryPolicy = retryPolicy;
        }

        protected override Task<CommandScope> OpenAsync(string commandText, CommandType commandType,
            object? parameters, CancellationToken ct) {
            var command = CreateCommand(_connection, _transaction, commandText, commandType, parameters);
            return Task.FromResult(new CommandScope(command, null));
        }

        // Retrying a single statement inside a transaction is not safe, so the action runs once.
        protected override async Task<T> RunAsync<T>(string commandText, CommandType commandType,
            Func<CancellationToken, Task<T>> action, CancellationToken ct) {
            return await action(ct);
        }

        public override IStoredProcedureBuilder StoredProcedure(string procedureName) {
            return new StoredProcedureBuilder(_connection, _transaction, _retryPolicy, Options, procedureName);
        }

        public override IBulkCopyBuilder BulkCopy(string destinationTable) {
            return new BulkCopyBuilder(_connection, _transaction, _retryPolicy, destinationTable);
        }

        public async Task CommitAsync(CancellationToken ct = default) {
            await _transaction.CommitAsync(ct);
        }

        public async Task RollbackAsync(CancellationToken ct = default) {
            await _transaction.RollbackAsync(ct);
        }

        public async ValueTask DisposeAsync() {
            if (_disposed) {
                return;
            }

            _disposed = true;

            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
        }

    }
}
