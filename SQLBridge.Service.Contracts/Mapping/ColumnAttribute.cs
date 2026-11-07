namespace SQLBridge.Service.Contracts.Mapping {
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ColumnAttribute : Attribute {

        public string Name { get; }

        public ColumnAttribute(string name) {
            Name = name;
        }

    }
}
