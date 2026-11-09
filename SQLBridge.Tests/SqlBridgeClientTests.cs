using System.Data;

namespace SQLBridge.Tests {
    public class SqlBridgeClientTests {

        private const string ConnectionString = "Server=localhost;Database=Test;Integrated Security=true;TrustServerCertificate=true";

        private sealed class AnswerInterceptor : QueryInterceptorBase {

            public override ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default) {
                return ValueTask.FromResult<InterceptResult?>(InterceptResult.WithValue(context.CommandText));
            }

        }

        private static void CreateWithoutConnectionString() {
            new SqlBridgeClient(string.Empty);
        }

        [Fact]
        public void New_WithConnectionStringOnly_NeedsNoDependencyInjection() {
            using var client = new SqlBridgeClient(ConnectionString);

            Assert.Equal(0, client.GetCacheStats().EntryCount);
            Assert.Null(client.GetPoolStats());
        }

        [Fact]
        public void New_WithoutConnectionString_Throws() {
            Assert.Throws<InvalidOperationException>(CreateWithoutConnectionString);
        }

        [Fact]
        public async Task Interceptors_CanBeSetOnOptionsWithoutDependencyInjection() {
            var options = new SqlBridgeOptions { ConnectionString = ConnectionString };
            options.Interceptors.Add(new AnswerInterceptor());
            using var client = new SqlBridgeClient(options);

            var result = await client.ScalarAsync("usp_GET_Policy");

            Assert.Equal("usp_GET_Policy", result);
        }

        [Fact]
        public void ClientAndTransaction_ShareTheSameOperations() {
            Assert.True(typeof(ISqlOperations).IsAssignableFrom(typeof(ISqlClient)));
            Assert.True(typeof(ISqlOperations).IsAssignableFrom(typeof(ISqlTransaction)));
        }

        [Theory]
        [InlineData("usp_GET_Policy", CommandType.StoredProcedure)]
        [InlineData("dbo.usp_GET_Policy", CommandType.StoredProcedure)]
        [InlineData("SELECT 1", CommandType.Text)]
        [InlineData("  EXEC usp_GET_Policy @Id = 1", CommandType.Text)]
        public void CommandTypeResolver_TellsProceduresFromSql(string commandText, CommandType expected) {
            Assert.Equal(expected, CommandTypeResolver.Resolve(commandText));
        }

    }
}
