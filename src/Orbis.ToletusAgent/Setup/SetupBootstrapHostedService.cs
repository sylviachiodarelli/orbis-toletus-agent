using Orbis.ToletusAgent.Configuration;

namespace Orbis.ToletusAgent.Setup;

public sealed class SetupBootstrapHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IAgentConfigurationService _configurationService;
    private readonly ILogger<SetupBootstrapHostedService> _logger;

    public SetupBootstrapHostedService(
        IConfiguration configuration,
        IAgentConfigurationService configurationService,
        ILogger<SetupBootstrapHostedService> logger)
    {
        _configuration = configuration;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (ConfigurationReadiness.IsSetupComplete(_configuration))
        {
            return;
        }

        if (!ConfigurationReadiness.HasMinimumRuntimeConfig(_configuration))
        {
            _logger.LogInformation(
                "Agent setup is incomplete. Open the setup UI to configure Orbisfit and the turnstile.");
            return;
        }

        _logger.LogInformation(
            "Detected existing configuration. Marking setup as complete for backward compatibility.");

        await _configurationService.SaveAsync(_configurationService.GetCurrentSetup(), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
