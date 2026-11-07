namespace SQLBridge {
    /// <summary>
    /// Maps a property to a column whose name differs from the property name. Used when reading
    /// query results and when bulk copying.
    /// </summary>
    /// <example>
    /// <code>
    /// [Column("effective_date")]
    /// public DateTime EffectiveDate { get; set; }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ColumnAttribute : Attribute {

        /// <summary>The column name in the result set or destination table.</summary>
        public string Name { get; }

        /// <param name="name">The column name.</param>
        public ColumnAttribute(string name) {
            Name = name;
        }

    }
}
