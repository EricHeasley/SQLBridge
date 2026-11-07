namespace SQLBridge {
    public sealed class StoredProcedureResult<T> {
        public IReadOnlyList<T> Data { get; init; } = [];
        public int RowsAffected { get; init; }
        public OutputParameterCollection OutputParams { get; init; } = new();
    }
}
