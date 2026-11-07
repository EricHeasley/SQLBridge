using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SQLBridge {
    public static class ServiceCollectionExtensions {

        /// <summary>
        /// Registers <see cref="ISqlClient"/> with the specified connection string.
        /// </summary>
        public static IServiceCollection AddSqlBridge(
            this IServiceCollection services,
            string connectionString) {
            return services.AddSqlBridge(options => {
                options.ConnectionString = connectionString;
            });
        }

        /// <summary>
        /// Registers <see cref="ISqlClient"/> from a configuration section (default "SqlBridge"),
        /// falling back to ConnectionStrings:DefaultConnection when the section has no connection string.
        /// </summary>
        public static IServiceCollection AddSqlBridge(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "SqlBridge") {
            var section = configuration.GetSection(sectionName);
            services.Configure<SqlBridgeOptions>(section);

            services.PostConfigure<SqlBridgeOptions>(options => {
                if (string.IsNullOrEmpty(options.ConnectionString)) {
                    options.ConnectionString =
                        configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException(
                            $"SQLBridge connection string is not configured. " +
                            $"Set {sectionName}:ConnectionString or ConnectionStrings:DefaultConnection.");
                }
            });

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
            services.TryAddSingleton<ISqlClient>(provider => new SqlBridge(
                provider.GetRequiredService<IOptions<SqlBridgeOptions>>().Value,
                provider.GetService<ILoggerFactory>(),
                provider.GetServices<IQueryInterceptor>()));
        }

    }
}
