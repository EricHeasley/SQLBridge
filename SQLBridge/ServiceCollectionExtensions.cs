using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SQLBridge {
    /// <summary>
    /// Dependency injection registration for SQLBridge.
    /// </summary>
    public static class ServiceCollectionExtensions {

        /// <summary>
        /// Registers <see cref="ISqlClient"/> with the specified connection string.
        /// </summary>
        public static IServiceCollection AddSqlBridge(
            this IServiceCollection services,
            string connectionString) {
            services.AddOptions();
            services.AddSingleton<IConfigureOptions<SqlBridgeOptions>>(new SqlBridgeConnectionStringSetup(connectionString));
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Registers <see cref="ISqlClient"/> from a configuration section (default "SqlBridge"),
        /// falling back to ConnectionStrings:DefaultConnection when the section has no connection string.
        /// </summary>
        public static IServiceCollection AddSqlBridge(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "SqlBridge") {
            services.AddOptions();
            services.AddSingleton<IConfigureOptions<SqlBridgeOptions>>(new SqlBridgeConfigurationSetup(configuration, sectionName));
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Registers <see cref="ISqlClient"/> with full options configuration: caching, retry,
        /// pool monitoring, interceptors and diagnostics are all options.
        /// </summary>
        public static IServiceCollection AddSqlBridge(
            this IServiceCollection services,
            Action<SqlBridgeOptions> configure) {
            services.Configure(configure);
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Registers an interceptor that needs dependency injection. Interceptors that need nothing
        /// can be added to <see cref="SqlBridgeOptions.Interceptors"/> instead.
        /// </summary>
        public static IServiceCollection AddSqlBridgeInterceptor<T>(this IServiceCollection services)
            where T : class, IQueryInterceptor {
            services.AddSingleton<IQueryInterceptor, T>();
            return services;
        }

        /// <summary>
        /// Adds a health check for the SQL Server connection.
        /// </summary>
        public static IHealthChecksBuilder AddSqlBridgeHealthCheck(
            this IHealthChecksBuilder builder,
            string name = "sqlbridge",
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null) {
            return builder.AddCheck<SqlBridgeHealthCheck>(name, failureStatus, tags ?? []);
        }

        private static void RegisterServices(IServiceCollection services) {
            services.TryAddSingleton<ISqlClient>(CreateSqlBridge);
        }

        private static ISqlClient CreateSqlBridge(IServiceProvider provider) {
            return new SqlBridgeClient(
                provider.GetRequiredService<IOptions<SqlBridgeOptions>>().Value,
                provider.GetService<ILoggerFactory>(),
                provider.GetServices<IQueryInterceptor>());
        }

    }
}
