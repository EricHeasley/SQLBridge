using System.Data;

namespace SQLBridge.Service.Contracts.BulkOperations {
    public interface IBulkCopyBuilder {

        IBulkCopyBuilder WithTimeout(int seconds);
        IBulkCopyBuilder WithBatchSize(int batchSize);
        IBulkCopyBuilder MapColumn(string source, string destination);
        Task WriteAsync<T>(IEnumerable<T> data, CancellationToken ct = default);
        Task WriteAsync(DataTable data, CancellationToken ct = default);
        Task WriteAsync(IDataReader reader, CancellationToken ct = default);

    }
}
