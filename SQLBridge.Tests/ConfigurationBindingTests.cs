using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SQLBridge.Tests {
    public class ConfigurationBindingTests {

        private static SqlBridgeOptions Resolve(Dictionary<string, string?> values, string sectionName = "SqlBridge") {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection();
            services.AddSqlBridge(configuration, sectionName);
            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IOptions<SqlBridgeOptions>>().Value;
        }

        private static void ResolveWithoutConnectionString() {
            Resolve(new Dictionary<string, string?> { ["SqlBridge:DefaultCommandTimeout"] = "10" });
        }

        [Fact]
        public void AddSqlBridge_BindsTheSection() {
            var options = Resolve(new Dictionary<string, string?> {
                ["SqlBridge:ConnectionString"] = "Server=a",
                ["SqlBridge:DefaultCommandTimeout"] = "90",
                ["SqlBridge:EnablePoolMonitoring"] = "true",
                ["SqlBridge:Retry:MaxRetryCount"] = "2",
                ["SqlBridge:Caching:DefaultTtl"] = "00:10:00"
            });

            Assert.Equal("Server=a", options.ConnectionString);
            Assert.Equal(90, options.DefaultCommandTimeout);
            Assert.True(options.EnablePoolMonitoring);
            Assert.Equal(2, options.Retry.MaxRetryCount);
            Assert.Equal(TimeSpan.FromMinutes(10), options.Caching.DefaultTtl);
        }

        [Fact]
        public void AddSqlBridge_FallsBackToDefaultConnection() {
            var options = Resolve(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = "Server=fallback"
            });

            Assert.Equal("Server=fallback", options.ConnectionString);
        }

        [Fact]
        public void AddSqlBridge_UsesTheNamedSection() {
            var options = Resolve(new Dictionary<string, string?> {
                ["Insurance:Sql:ConnectionString"] = "Server=named"
            }, "Insurance:Sql");

            Assert.Equal("Server=named", options.ConnectionString);
        }

        [Fact]
        public void AddSqlBridge_ThrowsWhenNoConnectionStringAnywhere() {
            Assert.Throws<InvalidOperationException>(ResolveWithoutConnectionString);
        }

    }
}
