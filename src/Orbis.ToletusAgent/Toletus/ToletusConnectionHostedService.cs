using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Setup;
using Orbis.ToletusAgent.Configuration;

namespace Orbis.ToletusAgent.Toletus;

public sealed class ToletusConnectionHostedService : BackgroundService
{
    private const int InitialReconnectDelayMs = 500;
    private const int MaxReconnectDelayMs = 15_000;

    private readonly ILogger<ToletusConnectionHostedService> _logger;
    private readonly IToletusDeviceService _deviceService;
    private readonly IAgentConfigurationService _configurationService;
    private readonly IOptionsMonitor<ToletusOptions> _optionsMonitor;

    public ToletusConnectionHostedService(
        ILogger<ToletusConnectionHostedService> logger,
        IToletusDeviceService deviceService,
        IAgentConfigurationService configurationService,
        IOptionsMonitor<ToletusOptions> optionsMonitor)
    {
        _logger = logger;
        _deviceService = deviceService;
        _configurationService = configurationService;
        _optionsMonitor = optionsMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconnectDelayMs = InitialReconnectDelayMs;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_configurationService.IsConfigured)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var options = _optionsMonitor.CurrentValue;
                if (string.IsNullOrWhiteSpace(options.Ip))
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (_deviceService.IsConnected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await _deviceService.ConnectAsync(stoppingToken).ConfigureAwait(false);
                    reconnectDelayMs = InitialReconnectDelayMs;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to connect to Toletus board at {TurnstileIp}. Retrying in {DelayMs}ms.",
                        options.Ip,
                        reconnectDelayMs);

                    await Task.Delay(reconnectDelayMs, stoppingToken).ConfigureAwait(false);
                    reconnectDelayMs = Math.Min(reconnectDelayMs * 2, MaxReconnectDelayMs);
                }
            }
        }
        finally
        {
            try
            {
                await _deviceService.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while disconnecting from Toletus board.");
            }
        }
    }
}
