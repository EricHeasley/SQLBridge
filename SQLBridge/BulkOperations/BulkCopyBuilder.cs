using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace SQLBridge {
    internal sealed class BulkCopyBuilder : IBulkCopyBuilder {

        private readonly ISqlConnectionFactory? _connectionFactory;
        private readonly SqlConnection? _transactionConnection;
        private readonly SqlTransaction? _transaction;
        private readonly RetryPolicy _retryPolicy;
        private readonly string _destinationTable;
        private int _timeout = 30;
        private int _batchSize = 1000;
        private readonly List<(string Source, string Destination)> _columnMappings = [];

        internal BulkCopyBuilder(
            ISqlConnectionFactory connectionFactory,
            RetryPolicy retryPolicy,
            string destinationTable) {
            _connectionFactory = connectionFactory;
            _retryPolicy = retryPolicy;
            _destinationTable = destinationTable;
        }

        // Bulk copy inside a transaction: uses the transaction's connection and never retries
        internal BulkCopyBuilder(
            SqlConnection connection,
            SqlTransaction transaction,
            RetryPolicy retryPolicy,
            string destinationTable) {
            _transactionConnection = connection;
            _transaction = transaction;
            _retryPolicy = retryPolicy;
            _destinationTable = destinationTable;
        }

        public IBulkCopyBuilder WithTimeout(int seconds) {
            _timeout = seconds;
            return this;
        }

        public IBulkCopyBuilder WithBatchSize(int batchSize) {
            _batchSize = batchSize;
            return this;
        }

        public IBulkCopyBuilder MapColumn(string source, string destination) {
            _columnMappings.Add((source, destination));
            return this;
        }

        public async Task WriteAsync<T>(IEnumerable<T> data, CancellationToken ct = default) {
            var dataTable = ToDataTable(data);
            await WriteAsync(dataTable, ct);
        }

        public async Task WriteAsync(DataTable data, CancellationToken ct = default) {
            await RunAsync(async (bulkCopy, token) => {
                ApplyMappings(bulkCopy, data);
                await bulkCopy.WriteToServerAsync(data, token);
            }, ct);
        }

        public async Task WriteAsync(IDataReader reader, CancellationToken ct = default) {
            await RunAsync(async (bulkCopy, token) => {
                foreach (var (source, dest) in _columnMappings) {
                    bulkCopy.ColumnMappings.Add(source, dest);
                }

                await bulkCopy.WriteToServerAsync(reader, token);
            }, ct);
        }

        private async Task RunAsync(Func<SqlBulkCopy, CancellationToken, Task> write, CancellationToken ct) {
            if (_transactionConnection is not null) {
                using var transactionalCopy = CreateBulkCopy(_transactionConnection, _transaction);
                await write(transactionalCopy, ct);
                return;
            }

            await _retryPolicy.ExecuteAsync(async token => {
                await using var connection = await _connectionFactory!.CreateOpenConnectionAsync(token);
                using var bulkCopy = CreateBulkCopy(connection, null);
                await write(bulkCopy, token);
            }, ct);
        }

        private SqlBulkCopy CreateBulkCopy(SqlConnection connection, SqlTransaction? transaction) {
            return new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction) {
                DestinationTableName = _destinationTable,
                BulkCopyTimeout = _timeout,
                BatchSize = _batchSize
            };
        }

        private void ApplyMappings(SqlBulkCopy bulkCopy, DataTable data) {
            if (_columnMappings.Count > 0) {
                foreach (var (source, dest) in _columnMappings) {
                    bulkCopy.ColumnMappings.Add(source, dest);
                }
            } else {
                // Auto-map by column name
                foreach (DataColumn column in data.Columns) {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }
            }
        }

        private static DataTable ToDataTable<T>(IEnumerable<T> data) {
            var type = typeof(T);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && IsSimpleType(p.PropertyType))
                .ToArray();

            var table = new DataTable();
            foreach (var prop in properties) {
                var columnType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                // Check for [Column] attribute for custom name
                var attr = prop.GetCustomAttribute<ColumnAttribute>();
                var columnName = attr?.Name ?? prop.Name;

                table.Columns.Add(columnName, columnType);
            }

            foreach (var item in data) {
                var row = table.NewRow();
                for (var i = 0; i < properties.Length; i++) {
                    var value = properties[i].GetValue(item);
                    row[i] = value ?? DBNull.Value;
                }
                table.Rows.Add(row);
            }

            return table;
        }

        private static bool IsSimpleType(Type type) {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive
                || underlying.IsEnum
                || underlying == typeof(string)
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(DateTimeOffset)
                || underlying == typeof(TimeSpan)
                || underlying == typeof(Guid)
                || underlying == typeof(byte[]);
        }

    }
}
