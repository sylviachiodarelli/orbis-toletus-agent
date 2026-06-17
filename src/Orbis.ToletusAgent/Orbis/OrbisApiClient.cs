using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Health;

namespace Orbis.ToletusAgent.Orbis;

public interface IOrbisApiClient
{
    Task<AccessDecision> ValidateAccessAsync(
        AccessAttemptRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class OrbisApiClient : IOrbisApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OrbisOptions _orbisOptions;
    private readonly ToletusOptions _toletusOptions;
    private readonly IAgentHealthState _healthState;
    private readonly ILogger<OrbisApiClient> _logger;

    public OrbisApiClient(
        HttpClient httpClient,
        IOptions<OrbisOptions> orbisOptions,
        IOptions<ToletusOptions> toletusOptions,
        IAgentHealthState healthState,
        ILogger<OrbisApiClient> logger)
    {
        _httpClient = httpClient;
        _orbisOptions = orbisOptions.Value;
        _toletusOptions = toletusOptions.Value;
        _healthState = healthState;
        _logger = logger;
    }

    public Task<AccessDecision> ValidateAccessAsync(
        AccessAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = _orbisOptions.UseV2Endpoint
            ? _orbisOptions.AccessAttemptPath
            : _orbisOptions.LegacyValidationPath;

        var payload = _orbisOptions.UseV2Endpoint
            ? BuildV2Payload(request)
            : BuildV1Payload(request);

        return SendWithRetryAsync(path, payload, request.TransactionId, cancellationToken);
    }

    private async Task<AccessDecision> SendWithRetryAsync(
        string path,
        object payload,
        string transactionId,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _orbisOptions.MaxRetries + 1);
        Exception? lastException = null;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                var backoffMs = GetBackoffMs(attempt - 1);
                _logger.LogWarning(
                    "Retrying Orbisfit access validation for transaction {TransactionId} in {BackoffMs}ms (attempt {Attempt}/{MaxAttempts}).",
                    transactionId,
                    backoffMs,
                    attempt + 1,
                    maxAttempts);

                await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await SendOnceAsync(path, payload, transactionId, cancellationToken).ConfigureAwait(false);
            }
            catch (OrbisApiException ex) when (ex.StatusCode is 401 or 403)
            {
                throw;
            }
            catch (OrbisApiException ex) when (ex.StatusCode is >= 400 and < 500)
            {
                throw;
            }
            catch (Exception ex) when (IsRetriable(ex) && attempt < maxAttempts - 1)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Orbisfit access validation failed for transaction {TransactionId} (attempt {Attempt}/{MaxAttempts}).",
                    transactionId,
                    attempt + 1,
                    maxAttempts);
            }
        }

        throw new OrbisApiException(
            "Orbisfit access validation failed after retries.",
            innerException: lastException);
    }

    private async Task<AccessDecision> SendOnceAsync(
        string path,
        object payload,
        string transactionId,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_orbisOptions.TimeoutMs);

        var startedAt = DateTimeOffset.UtcNow;
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.TryAddWithoutValidation("x-api-key", _orbisOptions.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OrbisApiException("Orbisfit access validation timed out.", innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new OrbisApiException("Orbisfit access validation request failed.", innerException: ex);
        }

        var latencyMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Orbisfit access validation transaction {TransactionId} status {StatusCode} latency {LatencyMs}ms deviceIp {DeviceIp}.",
            transactionId,
            (int)response.StatusCode,
            latencyMs,
            _toletusOptions.Ip);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new OrbisApiException("Orbisfit API rejected the API key (401 Unauthorized).", 401);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new OrbisApiException("Orbisfit API denied access to this resource (403 Forbidden).", 403);
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new OrbisApiException(
                $"Orbisfit API returned server error {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OrbisApiException(
                $"Orbisfit API returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        try
        {
            _healthState.RecordSuccessfulValidation(DateTimeOffset.UtcNow);
            return AccessDecisionParser.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new OrbisApiException("Orbisfit API returned an invalid JSON response.", innerException: ex);
        }
    }

    private object BuildV2Payload(AccessAttemptRequest request)
    {
        return new
        {
            transaction_id = request.TransactionId,
            direction = request.Direction,
            device = new
            {
                device_code = request.DeviceCode,
                modelo = "toletus",
                ip = _toletusOptions.Ip
            },
            credential = new
            {
                type = request.CredentialType,
                value = request.CredentialValue
            },
            @event = new
            {
                kind = "ACCESS_ATTEMPT",
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            },
            raw_payload = BuildRawPayload(request.RawPayload)
        };
    }

    private Dictionary<string, object?> BuildV1Payload(AccessAttemptRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["credential_type"] = request.CredentialType,
            ["credential_value"] = request.CredentialValue,
            ["device_id"] = request.DeviceCode,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        switch (request.CredentialType.ToUpperInvariant())
        {
            case "ENROLLID":
                body["enrollid"] = request.CredentialValue;
                break;
            case "CPF":
                body["cpf"] = request.CredentialValue;
                break;
            case "RFID":
                body["rfid"] = request.CredentialValue;
                break;
        }

        return body;
    }

    private object BuildRawPayload(object? rawPayload)
    {
        if (rawPayload is null)
        {
            return new
            {
                vendor = "toletus",
                serial = _toletusOptions.SerialNumber,
                agent_version = GetAgentVersion()
            };
        }

        if (rawPayload is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            var dictionary = new Dictionary<string, object?>();
            foreach (var property in jsonElement.EnumerateObject())
            {
                dictionary[property.Name] = property.Value.ToString();
            }

            dictionary.TryAdd("vendor", "toletus");
            dictionary.TryAdd("serial", _toletusOptions.SerialNumber);
            dictionary.TryAdd("agent_version", GetAgentVersion());
            return dictionary;
        }

        return rawPayload;
    }

    private int GetBackoffMs(int retryIndex)
    {
        if (_orbisOptions.RetryBackoffMs.Length == 0)
        {
            return 500;
        }

        return retryIndex < _orbisOptions.RetryBackoffMs.Length
            ? _orbisOptions.RetryBackoffMs[retryIndex]
            : _orbisOptions.RetryBackoffMs[^1];
    }

    private static bool IsRetriable(Exception exception)
    {
        return exception is OrbisApiException { StatusCode: >= 500 }
            or HttpRequestException
            or TaskCanceledException;
    }

    private static string GetAgentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
    }
}
