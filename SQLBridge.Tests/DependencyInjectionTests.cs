using Microsoft.Extensions.DependencyInjection;
using SQLBridge.Caching;
using SQLBridge.Core;
using SQLBridge.Pool;

namespace SQLBridge.Tests {
    public class DependencyInjectionTests {

        private static ServiceProvider Build(Action<IServiceCollection>? configure = null) {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlClient("Server=localhost;Database=Test;Integrated Security=true;TrustServerCertificate=true");
            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void AddSqlClient_ResolvesSqlClientWithCachingByDefault() {
            using var provider = Build();

            var client = provider.GetRequiredService<ISqlClient>();

            Assert.NotNull(client.Cache);
            Assert.Null(client.Pool);
        }

        [Fact]
        public void AddSqlClientPoolMonitoring_ExposesPoolMonitor() {
            using var provider = Build(s => s.AddSqlClientPoolMonitoring());

            var client = provider.GetRequiredService<ISqlClient>();

            Assert.NotNull(client.Pool);
            Assert.IsAssignableFrom<IConnectionPoolMonitor>(client.Pool);
        }

        [Fact]
        public void AddSqlClientCaching_ReplacesDefaultCache() {
            using var provider = Build(s => s.AddSqlClientCaching(o => o.MaxEntries = 5));

            Assert.IsType<MemoryQueryCache>(provider.GetRequiredService<IQueryCache>());
        }

    }
}
