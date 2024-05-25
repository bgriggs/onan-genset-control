using BigMission.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace OnanGensetControl;

internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddNLog();
        });

        builder.Services.AddSingleton<IPinControlFactory, RpiPinControlFactory>();
        builder.Services.AddSingleton<IDateTimeHelper, DateTimeHelper>();
        builder.Services.AddHostedService<Application>();

        using IHost host = builder.Build();
        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(Program).GetType().Name);

        logger.LogInformation("Starting application");
        await host.RunAsync();
    }
}