using Microsoft.Data.SqlClient;

namespace SQLBridge.Tests {
    public class ParameterBuilderTests {

        private static string[] Names(SqlParameter[] parameters) {
            var names = new List<string>();
            foreach (var parameter in parameters) {
                names.Add(parameter.ParameterName);
            }

            return names.ToArray();
        }

        [Fact]
        public void FromObject_ReadsAnonymousObjectProperties() {
            var parameters = ParameterBuilder.FromObject(new { PolicyId = 7, HolderName = "Acme" });

            Assert.Equal(2, parameters.Length);
            Assert.Equal(7, parameters[Array.IndexOf(Names(parameters), "@PolicyId")].Value);
        }

        [Fact]
        public void FromObject_ConvertsNullToDbNull() {
            var parameters = ParameterBuilder.FromObject(new { HolderName = (string?)null });

            Assert.Equal(DBNull.Value, parameters[0].Value);
        }

        [Fact]
        public void FromObject_AcceptsDictionaryAndKeepsExistingAtPrefix() {
            var parameters = ParameterBuilder.FromObject(new Dictionary<string, object?> {
                ["@PolicyId"] = 1,
                ["Region"] = "TX"
            });

            Assert.Contains("@PolicyId", Names(parameters));
            Assert.Contains("@Region", Names(parameters));
        }

        [Fact]
        public void FromObject_PassesSqlParametersThrough() {
            var existing = new[] { new SqlParameter("@X", 1) };

            Assert.Same(existing, ParameterBuilder.FromObject(existing));
        }

        [Fact]
        public void FromObject_NullGivesEmptyArray() {
            Assert.Empty(ParameterBuilder.FromObject(null));
        }

    }
}
