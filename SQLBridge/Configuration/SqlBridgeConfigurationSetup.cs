using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SQLBridge {
    /// <summary>
    /// Binds <see cref="SqlBridgeOptions"/> from a configuration section, falling back to
    /// ConnectionStrings:DefaultConnection when the section has no connection string.
    /// </summary>
    internal sealed class SqlBridgeConfigurationSetup : IConfigureOptions<SqlBridgeOptions> {

        private readonly IConfiguration _configuration;
        private readonly string _sectionName;

        public SqlBridgeConfigurationSetup(IConfiguration configuration, string sectionName) {
            _configuration = configuration;
            _sectionName = sectionName;
        }

        public void Configure(SqlBridgeOptions options) {
            _configuration.GetSection(_sectionName).Bind(options);

            if (!string.IsNullOrEmpty(options.ConnectionString)) {
                return;
            }

            options.ConnectionString =
                _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    $"SQLBridge connection string is not configured. " +
                    $"Set {_sectionName}:ConnectionString or ConnectionStrings:DefaultConnection.");
        }

    }
}
