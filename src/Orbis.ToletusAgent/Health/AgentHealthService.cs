using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Health;

public sealed class AgentHealthService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<AgentHealthService> _logger;
    private readonly IToletusDeviceService _deviceService;
    private readonly IAgentHealthState _healthState;
    private readonly AgentOptions _agentOptions;
    private readonly ToletusOptions _toletusOptions;

    public AgentHealthService(
        ILogger<AgentHealthService> logger,
        IToletusDeviceService deviceService,
        IAgentHealthState healthState,
        IOptions<AgentOptions> agentOptions,
        IOptions<ToletusOptions> toletusOptions)
    {
        _logger = logger;
        _deviceService = deviceService;
        _healthState = healthState;
        _agentOptions = agentOptions.Value;
        _toletusOptions = toletusOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_agentOptions.HeartbeatIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var snapshot = BuildSnapshot();
            _logger.LogInformation(
                "Heartbeat agentVersion={AgentVersion} turnstileIp={DeviceIp} sdkConnected={SdkConnected} firmware={FirmwareVersion} serial={SerialNumber} lastValidationOkAt={LastValidationOkAt}",
                snapshot.AgentVersion,
                snapshot.TurnstileIp,
                snapshot.SdkConnected,
                snapshot.FirmwareVersion ?? "n/a",
                snapshot.SerialNumber ?? "n/a",
                snapshot.LastSuccessfulValidationAt?.ToString("O") ?? "never");

            await WriteHealthFileAsync(snapshot, stoppingToken).ConfigureAwait(false);
        }
    }

    private AgentHealthSnapshot BuildSnapshot()
    {
        return new AgentHealthSnapshot
        {
            AgentVersion = GetAgentVersion(),
            TurnstileIp = _toletusOptions.Ip,
            SdkConnected = _deviceService.IsConnected,
            FirmwareVersion = _deviceService.FirmwareVersion,
            SerialNumber = _deviceService.SerialNumber ?? _toletusOptions.SerialNumber,
            LastSuccessfulValidationAt = _healthState.LastSuccessfulValidationAt,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private async Task WriteHealthFileAsync(AgentHealthSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_agentOptions.HealthFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await File.WriteAllTextAsync(_agentOptions.HealthFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write health file {HealthFilePath}.", _agentOptions.HealthFilePath);
        }
    }

    private static string GetAgentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    private sealed class AgentHealthSnapshot
    {
        public string AgentVersion { get; init; } = string.Empty;

        public string TurnstileIp { get; init; } = string.Empty;

        public bool SdkConnected { get; init; }

        public string? FirmwareVersion { get; init; }

        public string? SerialNumber { get; init; }

        public DateTimeOffset? LastSuccessfulValidationAt { get; init; }

        public DateTimeOffset Timestamp { get; init; }
    }
}
