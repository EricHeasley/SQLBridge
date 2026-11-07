namespace SQLBridge {
    public sealed class StoredProcedureResult {
        public int RowsAffected { get; init; }
        public OutputParameterCollection OutputParams { get; init; } = new();
    }
}
