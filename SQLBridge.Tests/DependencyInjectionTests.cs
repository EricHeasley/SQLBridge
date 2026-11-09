using Microsoft.Extensions.DependencyInjection;

namespace SQLBridge.Tests {
    public class DependencyInjectionTests {

        private const string ConnectionString = "Server=localhost;Database=Test;Integrated Security=true;TrustServerCertificate=true";

        private static ServiceProvider Build() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlBridge(ConnectionString);
            return services.BuildServiceProvider();
        }

        private static void WithPoolMonitoring(SqlBridgeOptions options) {
            options.ConnectionString = ConnectionString;
            options.EnablePoolMonitoring = true;
        }

        private static ServiceProvider BuildWithPoolMonitoring() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlBridge(WithPoolMonitoring);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void AddSqlBridge_ResolvesSqlBridgeWithCachingByDefault() {
            using var provider = Build();

            var client = provider.GetRequiredService<ISqlClient>();

            Assert.IsType<SqlBridgeClient>(client);
            Assert.Equal(0, client.GetCacheStats().EntryCount);
            Assert.Null(client.GetPoolStats());
        }

        [Fact]
        public void EnablePoolMonitoring_ExposesPoolMonitor() {
            using var provider = BuildWithPoolMonitoring();

            var client = provider.GetRequiredService<ISqlClient>();

            var stats = client.GetPoolStats();
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalConnectionsOpened);
        }

        [Fact]
        public void AddSqlBridge_WithConnectionString_RegistersSingleClient() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlBridge(ConnectionString);
            using var provider = services.BuildServiceProvider();

            Assert.Same(provider.GetRequiredService<ISqlClient>(), provider.GetRequiredService<ISqlClient>());
        }

        [Fact]
        public async Task AddSqlBridgeInterceptor_IsPickedUpByTheClient() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlBridge(ConnectionString);
            services.AddSingleton<IQueryInterceptor, ShortCircuitingInterceptor>();
            using var provider = services.BuildServiceProvider();

            var client = provider.GetRequiredService<ISqlClient>();
            var result = await client.ScalarAsync("SELECT 1");

            Assert.Equal("intercepted", result);
        }

        private sealed class ShortCircuitingInterceptor : QueryInterceptorBase {

            public override ValueTask<InterceptResult?> BeforeExecuteAsync(CommandContext context, CancellationToken ct = default) {
                return ValueTask.FromResult<InterceptResult?>(InterceptResult.WithValue("intercepted"));
            }

        }

    }
}
