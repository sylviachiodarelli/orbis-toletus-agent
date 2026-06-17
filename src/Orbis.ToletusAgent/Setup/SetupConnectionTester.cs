using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Orbis.ToletusAgent.Setup;

public sealed record ConnectionTestResult(bool Success, string Message, string? DiscoveredSerial = null);

public sealed class SetupConnectionTester
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHostEnvironment _environment;

    public SetupConnectionTester(IHttpClientFactory httpClientFactory, IHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _environment = environment;
    }

    public async Task<ConnectionTestResult> TestOrbisAsync(
        string apiBaseUrl,
        string apiKey,
        string deviceCode,
        string turnstileIp,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new ConnectionTestResult(false, "URL da API inválida.");
        }

        var client = _httpClientFactory.CreateClient(nameof(SetupConnectionTester));
        client.BaseAddress = new Uri(baseUri.ToString().TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(8);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/turnstile/access-attempt");
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey.Trim());

        var payload = new
        {
            transaction_id = $"setup-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            direction = "IN",
            device = new
            {
                device_code = deviceCode.Trim(),
                modelo = "toletus",
                ip = turnstileIp.Trim()
            },
            credential = new { type = "ENROLLID", value = "0" },
            @event = new
            {
                kind = "ACCESS_ATTEMPT",
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new ConnectionTestResult(false, "API key rejeitada pelo Orbisfit (401/403).");
            }

            if ((int)response.StatusCode >= 500)
            {
                return new ConnectionTestResult(false, $"Orbisfit indisponível (HTTP {(int)response.StatusCode}).");
            }

            return new ConnectionTestResult(
                true,
                $"Orbisfit respondeu HTTP {(int)response.StatusCode}. Credenciais parecem válidas.");
        }
        catch (TaskCanceledException)
        {
            return new ConnectionTestResult(false, "Tempo esgotado ao contactar o Orbisfit.");
        }
        catch (HttpRequestException ex)
        {
            return new ConnectionTestResult(false, $"Falha de rede com Orbisfit: {ex.Message}");
        }
    }

    public async Task<ConnectionTestResult> TestToletusAsync(
        string turnstileIp,
        int port,
        CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(turnstileIp, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork)
        {
            return new ConnectionTestResult(false, "IP da catraca inválido.");
        }

        var targetPort = port > 0 ? port : 7878;

        try
        {
            using var tcp = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(4));

            await tcp.ConnectAsync(address, targetPort, connectCts.Token).ConfigureAwait(false);
            return new ConnectionTestResult(
                true,
                $"Catraca aceita conexão TCP na porta {targetPort}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectionTestResult(
                false,
                $"Sem resposta na porta {targetPort}. Verifique se o PC está na mesma rede da catraca.");
        }
        catch (SocketException ex)
        {
            return new ConnectionTestResult(false, $"Não foi possível conectar na catraca: {ex.Message}");
        }
    }
}
