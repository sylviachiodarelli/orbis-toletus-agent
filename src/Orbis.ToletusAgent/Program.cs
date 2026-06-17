using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Orbis.ToletusAgent;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Logging;
using Orbis.ToletusAgent.Setup;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    EnvironmentVariableMapper.Apply(builder.Configuration);
    SerilogConfiguration.Configure(builder);

    var statusUiEnabled = builder.Configuration.GetValue<bool>($"{AgentOptions.SectionName}:{nameof(AgentOptions.StatusUiEnabled)}", true);
    if (statusUiEnabled)
    {
        var bindAddress = builder.Configuration.GetValue<string>($"{AgentOptions.SectionName}:{nameof(AgentOptions.StatusUiBindAddress)}")
            ?? "127.0.0.1";
        var port = builder.Configuration.GetValue<int>($"{AgentOptions.SectionName}:{nameof(AgentOptions.StatusUiPort)}", 5080);
        builder.WebHost.UseUrls($"http://{bindAddress}:{port}");
    }

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "OrbisToletusAgent";
    });
    builder.Services.AddAgentServices(builder.Configuration);

    var app = builder.Build();
    app.MapSetupUi();

    if (statusUiEnabled)
    {
        var port = app.Configuration.GetValue<int>($"{AgentOptions.SectionName}:{nameof(AgentOptions.StatusUiPort)}", 5080);
        Log.Information("Setup UI available at http://127.0.0.1:{StatusUiPort}", port);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
