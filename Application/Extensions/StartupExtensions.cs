using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;

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
                    .ReadFrom.Configuration(configuration);


                if (Utilities.IsDevelopment)
                {
                    loggerConfig = loggerConfig.WriteTo.Console(
                        outputTemplate:"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Server} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                        .MinimumLevel.Debug();
                }

                _defaultLogger = loggerConfig.CreateLogger();
            }

            services.AddLogging(builder => builder.AddSerilog(_defaultLogger, dispose: true));
            return services;
        }
    }
}