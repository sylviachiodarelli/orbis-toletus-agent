using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Setup;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Health;

public sealed class AgentSelfHealingHostedService : BackgroundService
{
    private readonly ILogger<AgentSelfHealingHostedService> _logger;
    private readonly IToletusDeviceService _deviceService;
    private readonly IAgentConfigurationService _configurationService;
    private readonly IAgentRecoveryService _recoveryService;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly AgentRecoveryState _recoveryState;
    private readonly IOptionsMonitor<AgentOptions> _optionsMonitor;

    public AgentSelfHealingHostedService(
        ILogger<AgentSelfHealingHostedService> logger,
        IToletusDeviceService deviceService,
        IAgentConfigurationService configurationService,
        IAgentRecoveryService recoveryService,
        IHostApplicationLifetime lifetime,
        AgentRecoveryState recoveryState,
        IOptionsMonitor<AgentOptions> optionsMonitor)
    {
        _logger = logger;
        _deviceService = deviceService;
        _configurationService = configurationService;
        _recoveryService = recoveryService;
        _lifetime = lifetime;
        _recoveryState = recoveryState;
        _optionsMonitor = optionsMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var interval = Math.Max(10, options.SelfHealingCheckIntervalSeconds);

            try
            {
                await EvaluateAsync(options, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Self-healing check failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task EvaluateAsync(AgentOptions options, CancellationToken cancellationToken)
    {
        if (!options.SelfHealingEnabled || !_configurationService.IsConfigured)
        {
            return;
        }

        if (_deviceService.IsConnected)
        {
            _recoveryState.MarkSdkConnected();
            return;
        }

        _recoveryState.MarkSdkDisconnected();
        var (disconnectedSince, reconnectAttempts) = _recoveryState.Snapshot();
        if (disconnectedSince is null)
        {
            return;
        }

        var disconnectedFor = DateTimeOffset.UtcNow - disconnectedSince.Value;

        if (SelfHealingPolicy.ShouldRestartApplication(
                disconnectedFor,
                reconnectAttempts,
                options.MaxConsecutiveReconnectAttempts,
                options.ApplicationRestartAfterSeconds,
                options.SelfHealingEnabled))
        {
            _logger.LogWarning(
                "Self-healing restarting application after SDK disconnected for {DisconnectedSeconds}s with {Attempts} reconnect attempts.",
                (int)disconnectedFor.TotalSeconds,
                reconnectAttempts);

            _recoveryService.ScheduleApplicationRestart();
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            _lifetime.StopApplication();
            return;
        }

        if (!SelfHealingPolicy.ShouldForceReconnect(
                disconnectedFor,
                options.SdkForceReconnectAfterSeconds,
                options.SelfHealingEnabled))
        {
            return;
        }

        var lastReconnect = _recoveryState.LastReconnectAt;
        if (lastReconnect.HasValue
            && DateTimeOffset.UtcNow - lastReconnect.Value < TimeSpan.FromSeconds(options.SdkForceReconnectAfterSeconds))
        {
            return;
        }

        _logger.LogInformation(
            "Self-healing forcing turnstile reconnect after {DisconnectedSeconds}s disconnected.",
            (int)disconnectedFor.TotalSeconds);

        await _recoveryService.ReconnectTurnstileAsync(cancellationToken).ConfigureAwait(false);
    }
}
