namespace SQLBridge.Service.Contracts.StoredProcedures {
    public sealed class SprocResult {
        public int RowsAffected { get; init; }
        public OutputParameterCollection OutputParams { get; init; } = new();
    }
}
