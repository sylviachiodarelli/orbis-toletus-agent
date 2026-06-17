using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Setup;

namespace Orbis.ToletusAgent;

public sealed class AgentHostedService : BackgroundService
{
    private readonly ILogger<AgentHostedService> _logger;
    private readonly IAgentConfigurationService _configurationService;
    private readonly IOptionsMonitor<ToletusOptions> _toletusOptions;
    private readonly AgentOptions _agentOptions;

    public AgentHostedService(
        ILogger<AgentHostedService> logger,
        IAgentConfigurationService configurationService,
        IOptionsMonitor<ToletusOptions> toletusOptions,
        IOptions<AgentOptions> agentOptions)
    {
        _logger = logger;
        _configurationService = configurationService;
        _toletusOptions = toletusOptions;
        _agentOptions = agentOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configurationService.IsConfigured)
        {
            _logger.LogInformation(
                "Agent started in setup mode. Configure via the local UI at http://{BindAddress}:{StatusUiPort}",
                _agentOptions.StatusUiBindAddress,
                _agentOptions.StatusUiPort);
        }
        else
        {
            var toletus = _toletusOptions.CurrentValue;
            _logger.LogInformation(
                "Agent started. Turnstile target {TurnstileIp} (serial {SerialNumber}).",
                toletus.Ip,
                string.IsNullOrWhiteSpace(toletus.SerialNumber) ? "n/a" : toletus.SerialNumber);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Agent stopping.");
        }
    }
}
