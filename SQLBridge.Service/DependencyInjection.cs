using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SQLBridge.Service.Contracts.Caching;
using SQLBridge.Service.Contracts.Configuration;
using SQLBridge.Service.Caching;
using SQLBridge.Service.Contracts.Core;
using SQLBridge.Service.Contracts.Interceptors;
using SQLBridge.Service.Contracts.Pool;
using SQLBridge.Service.Core;
using SQLBridge.Service.Diagnostics;
using SQLBridge.Service.Interceptors;
using SQLBridge.Service.Pool;
using SQLBridge.Service.Resilience;

namespace SQLBridge.Service {
    public static class DependencyInjection {

        /// <summary>
        /// Registers ISqlClient with the specified connection string.
        /// </summary>
        public static IServiceCollection AddSqlClient(
            this IServiceCollection services,
            string connectionString) {
            return services.AddSqlClient(options => {
                options.ConnectionString = connectionString;
            });
        }

        /// <summary>
        /// Registers ISqlClient from a named configuration section.
        /// </summary>
        public static IServiceCollection AddSqlClient(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = "SqlClient") {
            var section = configuration.GetSection(sectionName);
            services.Configure<SqlClientOptions>(section);

            if (string.IsNullOrEmpty(section["ConnectionString"])) {
                services.PostConfigure<SqlClientOptions>(options => {
                    if (string.IsNullOrEmpty(options.ConnectionString)) {
                        options.ConnectionString =
                            configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException(
                                "SqlClient connection string is not configured. " +
                                "Set SqlClient:ConnectionString or ConnectionStrings:DefaultConnection.");
                    }
                });
            }

            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Registers ISqlClient with full options configuration.
        /// </summary>
        public static IServiceCollection AddSqlClient(
            this IServiceCollection services,
            Action<SqlClientOptions> configure) {
            services.Configure(configure);
            RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Enables in-memory query result caching for ISqlClient.
        /// Call after AddSqlClient().
        /// </summary>
        public static IServiceCollection AddSqlClientCaching(
            this IServiceCollection services,
            Action<CacheOptions>? configure = null) {
            var options = new CacheOptions();
            configure?.Invoke(options);
            services.Configure<CacheOptions>(o => {
                o.Enabled = options.Enabled;
                o.DefaultTtl = options.DefaultTtl;
                o.MaxEntries = options.MaxEntries;
                o.EvictionScanInterval = options.EvictionScanInterval;
            });

            // Replace the default cache with user-configured one
            services.RemoveAll<IQueryCache>();
            services.AddSingleton<IQueryCache>(
                new MemoryQueryCache(options.MaxEntries, options.EvictionScanInterval));

            return services;
        }

        /// <summary>
        /// Enables connection pool monitoring for ISqlClient.
        /// Call after AddSqlClient().
        /// </summary>
        public static IServiceCollection AddSqlClientPoolMonitoring(this IServiceCollection services) {
            services.AddSingleton<ConnectionPoolMonitor>();
            services.AddSingleton<IConnectionPoolMonitor>(sp => sp.GetRequiredService<ConnectionPoolMonitor>());
            return services;
        }

        /// <summary>
        /// Registers a query interceptor. Interceptors run in registration order (before)
        /// and reverse order (after/error). Call after AddSqlClient().
        /// </summary>
        public static IServiceCollection AddSqlClientInterceptor<T>(this IServiceCollection services)
            where T : class, IQueryInterceptor {
            services.AddSingleton<IQueryInterceptor, T>();
            services.AddSingleton<InterceptorPipeline>();
            return services;
        }

        /// <summary>
        /// Registers a query interceptor instance. Call after AddSqlClient().
        /// </summary>
        public static IServiceCollection AddSqlClientInterceptor(
            this IServiceCollection services,
            IQueryInterceptor interceptor) {
            services.AddSingleton(interceptor);
            services.AddSingleton<InterceptorPipeline>();
            return services;
        }

        /// <summary>
        /// Adds a health check for the SQL Server connection.
        /// </summary>
        public static IHealthChecksBuilder AddSqlClientHealthCheck(
            this IHealthChecksBuilder builder,
            string name = "sqlclient",
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null) {
            return builder.AddCheck<SqlClientHealthCheck>(name, failureStatus, tags ?? []);
        }

        private static void RegisterServices(IServiceCollection services) {
            services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddSingleton<RetryPolicy>();
            services.AddSingleton<ISqlClient, SqlClientDb>();

            // Auto-register caching with defaults (can be overridden via AddSqlClientCaching)
            services.TryAddSingleton<IQueryCache>(new MemoryQueryCache());
            services.TryAddSingleton(Microsoft.Extensions.Options.Options.Create(new CacheOptions()));
        }

    }
}
