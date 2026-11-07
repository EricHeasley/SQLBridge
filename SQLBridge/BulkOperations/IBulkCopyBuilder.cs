using System.Data;

namespace SQLBridge {
    /// <summary>
    /// Fluent builder for a bulk insert. Get one from <see cref="ISqlOperations.BulkCopy"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// await client.BulkCopy("Policy").WithBatchSize(5000).WriteAsync(policies);
    /// </code>
    /// </example>
    public interface IBulkCopyBuilder {

        /// <summary>Sets the bulk copy timeout in seconds. Default: 30.</summary>
        IBulkCopyBuilder WithTimeout(int seconds);

        /// <summary>Sets rows per batch. Default: 1000.</summary>
        IBulkCopyBuilder WithBatchSize(int batchSize);

        /// <summary>Maps a source column to a destination column. Without any mapping, columns match by name.</summary>
        IBulkCopyBuilder MapColumn(string source, string destination);

        /// <summary>
        /// Writes objects. Public simple-typed properties become columns; <see cref="ColumnAttribute"/> renames them.
        /// </summary>
        Task WriteAsync<T>(IEnumerable<T> data, CancellationToken ct = default);

        /// <summary>Writes a <see cref="DataTable"/>.</summary>
        Task WriteAsync(DataTable data, CancellationToken ct = default);

        /// <summary>Writes from a data reader.</summary>
        Task WriteAsync(IDataReader reader, CancellationToken ct = default);

    }
}
