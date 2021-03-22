using System;
using System.IO;
using System.Runtime.InteropServices;
using Data.MigrationContext;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using ILogger = Serilog.ILogger;

namespace IW4MAdmin.Application.Extensions
{
    public static class StartupExtensions
    {
        private static ILogger _defaultLogger = null;

        public static IServiceCollection AddBaseLogger(this IServiceCollection services,
            ApplicationConfiguration appConfig)
        {
            if (_defaultLogger == null)
            {
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(Path.Join(Utilities.OperatingDirectory, "Configuration", "LoggingConfiguration.json"))
                    .Build();

                var loggerConfig = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning);

                if (Utilities.IsDevelopment)
                {
                    loggerConfig = loggerConfig.WriteTo.Console(
                            outputTemplate:
                            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Server} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                        .MinimumLevel.Debug();
                }

                _defaultLogger = loggerConfig.CreateLogger();
            }

            services.AddLogging(builder => builder.AddSerilog(_defaultLogger, dispose: true));
            services.AddSingleton(new LoggerFactory()
                .AddSerilog(_defaultLogger, true));
            return services;
        }

        public static IServiceCollection AddDatabaseContextOptions(this IServiceCollection services,
            ApplicationConfiguration appConfig)
        {
            var activeProvider = appConfig.DatabaseProvider?.ToLower();

            if (string.IsNullOrEmpty(appConfig.ConnectionString) || activeProvider == "sqlite")
            {
                var currentPath = Utilities.OperatingDirectory;
                currentPath = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? $"{Path.DirectorySeparatorChar}{currentPath}"
                    : currentPath;

                var connectionStringBuilder = new SqliteConnectionStringBuilder
                    {DataSource = Path.Join(currentPath, "Database", "Database.db")};
                var connectionString = connectionStringBuilder.ToString();

                services.AddSingleton(sp => (DbContextOptions) new DbContextOptionsBuilder<SqliteDatabaseContext>()
                    .UseSqlite(connectionString)
                    .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .EnableSensitiveDataLogging().Options);
                return services;
            }

            switch (activeProvider)
            {
                case "mysql":
                    var appendTimeout = !appConfig.ConnectionString.Contains("default command timeout",
                        StringComparison.InvariantCultureIgnoreCase);
                    services.AddSingleton(sp => (DbContextOptions) new DbContextOptionsBuilder<MySqlDatabaseContext>()
                        .UseMySql(appConfig.ConnectionString + (appendTimeout ? ";default command timeout=0" : ""),
                            mysqlOptions => mysqlOptions.EnableRetryOnFailure())
                        .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>()).Options);
                    return services;
                case "postgresql":
                    appendTimeout = !appConfig.ConnectionString.Contains("Command Timeout",
                        StringComparison.InvariantCultureIgnoreCase);
                    services.AddSingleton(sp =>
                        (DbContextOptions) new DbContextOptionsBuilder<PostgresqlDatabaseContext>()
                            .UseNpgsql(appConfig.ConnectionString + (appendTimeout ? ";Command Timeout=0" : ""),
                                postgresqlOptions =>
                                {
                                    postgresqlOptions.EnableRetryOnFailure();
                                    postgresqlOptions.SetPostgresVersion(new Version("9.4"));
                                })
                            .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>()).Options);
                    return services;
                default:
                    throw new ArgumentException($"No context available for {appConfig.DatabaseProvider}");
            }
        }
    }
}