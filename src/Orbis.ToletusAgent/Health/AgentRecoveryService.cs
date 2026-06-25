using Orbis.ToletusAgent.Setup;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Health;

public sealed class AgentRecoveryService : IAgentRecoveryService
{
    private readonly ILogger<AgentRecoveryService> _logger;
    private readonly IToletusDeviceService _deviceService;
    private readonly IAgentConfigurationService _configurationService;
    private readonly AgentRecoveryState _recoveryState;

    public AgentRecoveryService(
        ILogger<AgentRecoveryService> logger,
        IToletusDeviceService deviceService,
        IAgentConfigurationService configurationService,
        AgentRecoveryState recoveryState)
    {
        _logger = logger;
        _deviceService = deviceService;
        _configurationService = configurationService;
        _recoveryState = recoveryState;
    }

    public async Task<RecoveryActionResult> ReconnectTurnstileAsync(CancellationToken cancellationToken = default)
    {
        if (!_configurationService.IsConfigured)
        {
            return new RecoveryActionResult(false, "Configure o agente antes de reconectar a catraca.");
        }

        try
        {
            _logger.LogInformation("Manual or automatic turnstile reconnect requested.");
            await _deviceService.ForceReconnectAsync(cancellationToken).ConfigureAwait(false);
            var connected = _deviceService.IsConnected;
            var message = connected
                ? "Catraca reconectada com sucesso."
                : "Tentativa de reconexão concluída; SDK ainda desconectado. Verifique cabo/rede e IP da catraca.";

            if (connected)
            {
                _recoveryState.MarkSdkConnected();
            }
            else
            {
                _recoveryState.RecordReconnectAttempt(false, message);
            }

            _recoveryState.RecordRecoveryMessage(message);
            return new RecoveryActionResult(connected, message, connected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Turnstile reconnect failed.");
            var message = "Falha ao reconectar a catraca. Tente novamente em instantes.";
            _recoveryState.RecordReconnectAttempt(false, message);
            return new RecoveryActionResult(false, message);
        }
    }

    public RecoveryActionResult ScheduleApplicationRestart()
    {
        const string message =
            "O agente será reiniciado automaticamente. A tela pode ficar indisponível por até 1 minuto.";

        _logger.LogWarning("Application restart requested from recovery service.");
        _recoveryState.RecordRecoveryMessage(message);
        return new RecoveryActionResult(true, message);
    }
}
