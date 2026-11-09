using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLBridge.Tests {
    public class SqlRequestTests {

        private const string ConnectionString = "Server=localhost;Database=Test;Integrated Security=true;TrustServerCertificate=true";

        private sealed class Policy {

            public int PolicyId { get; set; }

        }

        /// <summary>Answers every query itself, so no database is needed, and records what it saw.</summary>
        private sealed class FakeDatabaseInterceptor : QueryInterceptorBase {

            public int Calls { get; private set; }
            public CommandContext? Last { get; private set; }

            public override ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default) {
                Calls++;
                Last = context;
                IReadOnlyList<Policy> rows = [new Policy { PolicyId = 7 }];
                return ValueTask.FromResult<InterceptResult?>(InterceptResult.WithValue(rows));
            }

        }

        private static SqlBridgeClient CreateClient(FakeDatabaseInterceptor interceptor) {
            var options = new SqlBridgeOptions { ConnectionString = ConnectionString };
            options.Interceptors.Add(interceptor);
            return new SqlBridgeClient(options);
        }

        [Fact]
        public async Task Chain_ParametersReachTheInterceptor() {
            var interceptor = new FakeDatabaseInterceptor();
            using var client = CreateClient(interceptor);

            var rows = await client.Command("usp_GET_Policy")
                .WithParam("PolicyId", 7)
                .WithParams(new { Region = "TX" })
                .QueryAsync<Policy>();

            Assert.Equal(7, rows[0].PolicyId);
            Assert.Equal("usp_GET_Policy", interceptor.Last!.CommandText);
            Assert.Equal("StoredProcedure", interceptor.Last.CommandType);

            var seen = Assert.IsType<Dictionary<string, object?>>(interceptor.Last.Parameters);
            Assert.Equal(7, seen["@PolicyId"]);
            Assert.Equal("TX", seen["@Region"]);
        }

        [Fact]
        public async Task CacheFor_SecondIdenticalRequestIsServedFromTheCache() {
            var interceptor = new FakeDatabaseInterceptor();
            using var client = CreateClient(interceptor);

            await client.Command("usp_GET_Policy").WithParam("Region", "TX").CacheFor().QueryAsync<Policy>();
            await client.Command("usp_GET_Policy").WithParam("Region", "TX").CacheFor().QueryAsync<Policy>();

            Assert.Equal(1, interceptor.Calls);
            Assert.Equal(1, client.GetCacheStats().HitCount);
        }

        [Fact]
        public async Task CacheFor_DifferentParameterValuesAreCachedSeparately() {
            var interceptor = new FakeDatabaseInterceptor();
            using var client = CreateClient(interceptor);

            await client.Command("usp_GET_Policy").WithParam("Region", "TX").CacheFor().QueryAsync<Policy>();
            await client.Command("usp_GET_Policy").WithParam("Region", "CA").CacheFor().QueryAsync<Policy>();

            Assert.Equal(2, interceptor.Calls);
        }

        [Fact]
        public async Task WithoutCacheFor_EveryRequestRuns() {
            var interceptor = new FakeDatabaseInterceptor();
            using var client = CreateClient(interceptor);

            await client.Command("usp_GET_Policy").QueryAsync<Policy>();
            await client.Command("usp_GET_Policy").QueryAsync<Policy>();

            Assert.Equal(2, interceptor.Calls);
        }

        [Fact]
        public async Task InvalidateCache_RemovesAllResultsForTheCommand() {
            var interceptor = new FakeDatabaseInterceptor();
            using var client = CreateClient(interceptor);
            await client.Command("dbo.usp_GET_Policy").WithParam("Region", "TX").CacheFor().QueryAsync<Policy>();
            await client.Command("dbo.usp_GET_Policy").WithParam("Region", "CA").CacheFor().QueryAsync<Policy>();
            await client.Command("dbo.usp_GET_Claim").CacheFor().QueryAsync<Policy>();

            client.InvalidateCache("dbo.usp_GET_Policy");

            Assert.Equal(1, client.GetCacheStats().EntryCount);
        }

        [Fact]
        public async Task ClearCache_RemovesEverything() {
            var interceptor = new FakeDatabaseInterceptor();
            using var client = CreateClient(interceptor);
            await client.Command("usp_GET_Policy").CacheFor().QueryAsync<Policy>();

            client.ClearCache();

            Assert.Equal(0, client.GetCacheStats().EntryCount);
        }

        [Fact]
        public async Task CacheFor_ThrowsForTerminalCallsThatCannotBeCached() {
            using var client = new SqlBridgeClient(ConnectionString);
            InvalidOperationException? caught = null;

            try {
                await client.Command("usp_GET_Policy").CacheFor().ScalarAsync();
            } catch (InvalidOperationException ex) {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Contains("QueryAsync", caught.Message);
        }

        [Fact]
        public void AddMapper_RegistersAMapperForEveryRequest() {
            var options = new SqlBridgeOptions();
            var mapper = new FixedPolicyMapper();

            options.AddMapper(mapper);

            Assert.Same(mapper, options.Mappers[typeof(Policy)]);
        }

        private sealed class FixedPolicyMapper : IResultMapper<Policy> {

            public Policy Map(IDataRecord record) {
                return new Policy { PolicyId = 1 };
            }

        }

        [Fact]
        public void ParameterSpec_BuildsAFreshTypedSqlParameterEachTime() {
            var spec = new ParameterSpec("@Name", "Acme", SqlDbType.NVarChar, 100, ParameterDirection.Input);

            var first = spec.ToSqlParameter();
            var second = spec.ToSqlParameter();

            Assert.NotSame(first, second);
            Assert.Equal(SqlDbType.NVarChar, first.SqlDbType);
            Assert.Equal(100, first.Size);
            Assert.Equal("Acme", first.Value);
        }

        [Fact]
        public void ParameterSpec_OutputParametersCarryNoValue() {
            var spec = new ParameterSpec("@NewId", null, SqlDbType.Int, 0, ParameterDirection.Output);

            var parameter = spec.ToSqlParameter();

            Assert.Equal(ParameterDirection.Output, parameter.Direction);
            Assert.Null(parameter.Value);
        }

        [Fact]
        public void ParameterSpec_NullInputBecomesDbNull() {
            var parameter = ParameterSpec.Input("Region", null).ToSqlParameter();

            Assert.Equal(DBNull.Value, parameter.Value);
            Assert.Equal("@Region", parameter.ParameterName);
        }

        [Fact]
        public void CacheKey_IgnoresOutputParametersAndParameterOrder() {
            var first = SqlCall.From("usp_GET_Policy", null) with {
                Specs = [ParameterSpec.Input("A", 1), ParameterSpec.Input("B", 2)]
            };
            var second = SqlCall.From("usp_GET_Policy", null) with {
                Specs = [
                    ParameterSpec.Input("B", 2),
                    new ParameterSpec("@Out", null, SqlDbType.Int, 0, ParameterDirection.Output),
                    ParameterSpec.Input("A", 1)
                ]
            };

            Assert.Equal(CacheKeyBuilder.Build(first, typeof(Policy)), CacheKeyBuilder.Build(second, typeof(Policy)));
        }

        [Fact]
        public void CacheKey_DiffersByRowType() {
            var call = SqlCall.From("usp_GET_Policy", null);

            Assert.NotEqual(CacheKeyBuilder.Build(call, typeof(Policy)), CacheKeyBuilder.Build(call, typeof(string)));
        }

    }
}
