using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Orbis.ToletusAgent.Logging;

public static class SerilogConfiguration
{
    public static void Configure(IHostApplicationBuilder builder)
    {
        var agentSection = builder.Configuration.GetSection(AgentOptions.SectionName);
        var logDirectory = agentSection.GetValue<string>(nameof(AgentOptions.LogDirectory))
            ?? new AgentOptions().LogDirectory;

        Directory.CreateDirectory(logDirectory);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            var agentOptions = services.GetRequiredService<IOptions<AgentOptions>>().Value;

            loggerConfiguration
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.Console()
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: Path.Combine(agentOptions.LogDirectory, "agent-.json"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: agentOptions.LogFileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 14,
                    shared: true);
        });
    }
}
