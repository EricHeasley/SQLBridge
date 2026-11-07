using System.Data;

namespace SQLBridge.Tests {
    public class ReflectionResultMapperTests {

        private sealed class Policy {
            public int PolicyId { get; set; }
            public string? HolderName { get; set; }
            public decimal Premium { get; set; }
            [Column("effective_date")]
            public DateTime EffectiveDate { get; set; }
            public int? ClaimCount { get; set; }
        }

        private static DataTableReader BuildReader() {
            var table = new DataTable();
            table.Columns.Add("PolicyId", typeof(int));
            table.Columns.Add("HolderName", typeof(string));
            table.Columns.Add("Premium", typeof(decimal));
            table.Columns.Add("effective_date", typeof(DateTime));
            table.Columns.Add("ClaimCount", typeof(int));
            table.Rows.Add(1, "Acme", 1250.50m, new DateTime(2025, 1, 15), 3);
            table.Rows.Add(2, DBNull.Value, 99.99m, new DateTime(2025, 6, 1), DBNull.Value);
            return table.CreateDataReader();
        }

        [Fact]
        public void Map_PopulatesPropertiesByColumnName() {
            using var reader = BuildReader();
            var mapper = new ReflectionResultMapper<Policy>();
            reader.Read();

            var policy = mapper.Map(reader);

            Assert.Equal(1, policy.PolicyId);
            Assert.Equal("Acme", policy.HolderName);
            Assert.Equal(1250.50m, policy.Premium);
            Assert.Equal(3, policy.ClaimCount);
        }

        [Fact]
        public void Map_HonorsColumnAttribute() {
            using var reader = BuildReader();
            var mapper = new ReflectionResultMapper<Policy>();
            reader.Read();

            var policy = mapper.Map(reader);

            Assert.Equal(new DateTime(2025, 1, 15), policy.EffectiveDate);
        }

        [Fact]
        public void Map_LeavesNullsAsDefault() {
            using var reader = BuildReader();
            var mapper = new ReflectionResultMapper<Policy>();
            reader.Read();
            reader.Read();

            var policy = mapper.Map(reader);

            Assert.Null(policy.HolderName);
            Assert.Null(policy.ClaimCount);
        }

        [Fact]
        public void DynamicMapper_ReturnsColumnsAsExpando() {
            using var reader = BuildReader();
            var mapper = new DynamicResultMapper();
            reader.Read();

            var row = (IDictionary<string, object?>)mapper.Map(reader);

            Assert.Equal("Acme", row["HolderName"]);
        }

    }
}
