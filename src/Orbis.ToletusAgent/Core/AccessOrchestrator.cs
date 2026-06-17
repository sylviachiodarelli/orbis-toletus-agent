using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Health;
using Orbis.ToletusAgent.Orbis;
using Orbis.ToletusAgent.Status;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Core;

public interface IAccessOrchestrator
{
    Task ProcessAccessAttemptAsync(ToletusAccessEvent accessEvent, CancellationToken cancellationToken = default);
}

public sealed class AccessOrchestrator : IAccessOrchestrator, IHostedService
{
    private readonly ILogger<AccessOrchestrator> _logger;
    private readonly IToletusDeviceService _deviceService;
    private readonly IOrbisApiClient _orbisApiClient;
    private readonly IOfflinePolicyCache _offlinePolicyCache;
    private readonly IAgentHealthState _healthState;
    private readonly IAgentActivityStore _activityStore;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly OrbisOptions _orbisOptions;
    private readonly ToletusOptions _toletusOptions;
    private readonly AgentOptions _agentOptions;
    private readonly object _debounceLock = new();
    private string? _lastCredentialKey;
    private DateTimeOffset _lastCredentialAt;

    public AccessOrchestrator(
        ILogger<AccessOrchestrator> logger,
        IToletusDeviceService deviceService,
        IOrbisApiClient orbisApiClient,
        IOfflinePolicyCache offlinePolicyCache,
        IAgentHealthState healthState,
        IAgentActivityStore activityStore,
        IHostApplicationLifetime lifetime,
        IOptions<OrbisOptions> orbisOptions,
        IOptions<ToletusOptions> toletusOptions,
        IOptions<AgentOptions> agentOptions)
    {
        _logger = logger;
        _deviceService = deviceService;
        _orbisApiClient = orbisApiClient;
        _offlinePolicyCache = offlinePolicyCache;
        _healthState = healthState;
        _activityStore = activityStore;
        _lifetime = lifetime;
        _orbisOptions = orbisOptions.Value;
        _toletusOptions = toletusOptions.Value;
        _agentOptions = agentOptions.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _deviceService.AccessAttemptReceived += OnAccessAttemptReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _deviceService.AccessAttemptReceived -= OnAccessAttemptReceived;
        return Task.CompletedTask;
    }

    public async Task ProcessAccessAttemptAsync(
        ToletusAccessEvent accessEvent,
        CancellationToken cancellationToken = default)
    {
        if (IsDebounced(accessEvent))
        {
            _logger.LogDebug(
                "Debounced duplicate access attempt {CredentialType} {CredentialValue}.",
                accessEvent.CredentialType,
                CredentialMapper.MaskCredentialForLog(
                    accessEvent.CredentialType.ToString(),
                    accessEvent.CredentialValue));
            return;
        }

        var request = CredentialMapper.Map(
            accessEvent,
            _orbisOptions,
            _toletusOptions,
            _deviceService.FirmwareVersion);

        var maskedValue = CredentialMapper.MaskCredentialForLog(request.CredentialType, request.CredentialValue);

        _logger.LogInformation(
            "Processing access attempt transaction {TransactionId} credential {CredentialType} value {CredentialValue}.",
            request.TransactionId,
            request.CredentialType,
            maskedValue);

        try
        {
            var decision = await _orbisApiClient.ValidateAccessAsync(request, cancellationToken).ConfigureAwait(false);
            _offlinePolicyCache.UpdateFromDecision(decision.OfflineMode);

            if (decision.Authorized)
            {
                await _deviceService.ReleaseTurnstileAsync(decision.Message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _deviceService.DenyTurnstileAsync(decision.Message, cancellationToken).ConfigureAwait(false);
            }

            RecordActivity(
                request.TransactionId,
                request.CredentialType,
                maskedValue,
                decision.Authorized,
                decision.Authorized ? "granted" : "denied",
                decision.Message,
                decision.StudentName);

            _logger.LogInformation(
                "Access processed transaction {TransactionId} credential {CredentialType} authorized {Authorized} deviceIp {DeviceIp} student {StudentName}.",
                request.TransactionId,
                request.CredentialType,
                decision.Authorized,
                _toletusOptions.Ip,
                decision.StudentName ?? "n/a");
        }
        catch (OrbisApiException ex) when (ex.StatusCode is 401 or 403)
        {
            _logger.LogError(
                ex,
                "Orbisfit authentication failed for transaction {TransactionId}. Denying access.",
                request.TransactionId);
            await SafeDenyAsync("Configuração inválida", cancellationToken).ConfigureAwait(false);
            RecordActivity(
                request.TransactionId,
                request.CredentialType,
                maskedValue,
                false,
                "error",
                "Configuração inválida",
                null);
        }
        catch (Exception ex) when (IsApiUnavailable(ex))
        {
            var authorized = _offlinePolicyCache.ResolveOfflineAuthorized();
            _logger.LogWarning(
                ex,
                "Orbisfit unavailable for transaction {TransactionId}. Applying offline policy authorized={Authorized}.",
                request.TransactionId,
                authorized);

            if (authorized)
            {
                await _deviceService.ReleaseTurnstileAsync("Modo offline", cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _deviceService.DenyTurnstileAsync("Sem conexão", cancellationToken).ConfigureAwait(false);
            }

            RecordActivity(
                request.TransactionId,
                request.CredentialType,
                maskedValue,
                authorized,
                "offline",
                authorized ? "Modo offline" : "Sem conexão",
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error processing transaction {TransactionId}.",
                request.TransactionId);
            await SafeDenyAsync("Erro interno", cancellationToken).ConfigureAwait(false);
            RecordActivity(
                request.TransactionId,
                request.CredentialType,
                maskedValue,
                false,
                "error",
                "Erro interno",
                null);
        }
    }

    private void OnAccessAttemptReceived(object? sender, ToletusAccessEvent accessEvent)
    {
        var stoppingToken = _lifetime.ApplicationStopping;
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessAccessAttemptAsync(accessEvent, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is shutting down.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled access orchestration failure.");
            }
        }, stoppingToken);
    }

    private bool IsDebounced(ToletusAccessEvent accessEvent)
    {
        var (credentialType, credentialValue) = CredentialMapper.MapCredential(accessEvent);
        var key = $"{credentialType}:{credentialValue}";
        var now = DateTimeOffset.UtcNow;

        lock (_debounceLock)
        {
            if (_lastCredentialKey == key
                && (now - _lastCredentialAt).TotalMilliseconds < _agentOptions.DebounceMs)
            {
                return true;
            }

            _lastCredentialKey = key;
            _lastCredentialAt = now;
            return false;
        }
    }

    private static bool IsApiUnavailable(Exception exception)
    {
        return exception is OrbisApiException { StatusCode: null or >= 500 }
            or HttpRequestException
            or TaskCanceledException;
    }

    private void RecordActivity(
        string transactionId,
        string credentialType,
        string credentialValueMasked,
        bool? authorized,
        string outcome,
        string? message,
        string? studentName)
    {
        _activityStore.Record(new AccessActivityEntry(
            DateTimeOffset.UtcNow,
            transactionId,
            credentialType,
            credentialValueMasked,
            authorized,
            outcome,
            message,
            studentName));
    }

    private async Task SafeDenyAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await _deviceService.DenyTurnstileAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send deny command to turnstile.");
        }
    }
}
