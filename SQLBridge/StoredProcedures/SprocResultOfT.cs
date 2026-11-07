namespace SQLBridge.StoredProcedures {
    public sealed class SprocResult<T> {
        public IReadOnlyList<T> Data { get; init; } = [];
        public int RowsAffected { get; init; }
        public OutputParameterCollection OutputParams { get; init; } = new();
    }
}
