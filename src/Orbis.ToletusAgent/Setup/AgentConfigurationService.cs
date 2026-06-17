using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;

namespace Orbis.ToletusAgent.Setup;

public sealed record AgentSetupDto(
    [property: JsonPropertyName("apiBaseUrl")] string ApiBaseUrl,
    [property: JsonPropertyName("apiKey")] string ApiKey,
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("toletusIp")] string ToletusIp,
    [property: JsonPropertyName("toletusSerialNumber")] string? ToletusSerialNumber);

public sealed record SetupValidationResult(bool Success, IReadOnlyList<string> Errors);

public interface IAgentConfigurationService
{
    bool IsConfigured { get; }

    AgentSetupDto GetCurrentSetup();

    SetupValidationResult Validate(AgentSetupDto setup);

    Task SaveAsync(AgentSetupDto setup, CancellationToken cancellationToken = default);
}

public sealed class AgentConfigurationService : IAgentConfigurationService
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new() { WriteIndented = true };

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<AgentOptions> _agentOptions;

    public AgentConfigurationService(
        IConfiguration configuration,
        IHostEnvironment environment,
        IOptionsMonitor<AgentOptions> agentOptions)
    {
        _configuration = configuration;
        _environment = environment;
        _agentOptions = agentOptions;
    }

    public bool IsConfigured =>
        ConfigurationReadiness.IsSetupComplete(_configuration)
        || ConfigurationReadiness.HasMinimumRuntimeConfig(_configuration);

    public AgentSetupDto GetCurrentSetup()
    {
        var orbis = _configuration.GetSection(OrbisOptions.SectionName);
        var toletus = _configuration.GetSection(ToletusOptions.SectionName);

        return new AgentSetupDto(
            ApiBaseUrl: orbis[nameof(OrbisOptions.ApiBaseUrl)] ?? "https://orbisfit.com",
            ApiKey: orbis[nameof(OrbisOptions.ApiKey)] ?? string.Empty,
            DeviceCode: orbis[nameof(OrbisOptions.DeviceCode)] ?? string.Empty,
            ToletusIp: toletus[nameof(ToletusOptions.Ip)] ?? string.Empty,
            ToletusSerialNumber: toletus[nameof(ToletusOptions.SerialNumber)]);
    }

    public SetupValidationResult Validate(AgentSetupDto setup)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(setup.ApiBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps
                && !(_environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp)))
        {
            errors.Add("URL da API Orbisfit inválida (use HTTPS).");
        }

        if (string.IsNullOrWhiteSpace(setup.ApiKey))
        {
            errors.Add("API key é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(setup.DeviceCode))
        {
            errors.Add("Código do dispositivo é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(setup.ToletusIp))
        {
            errors.Add("IP da catraca é obrigatório.");
        }
        else if (!IPAddress.TryParse(setup.ToletusIp, out var address)
                 || address.AddressFamily != AddressFamily.InterNetwork)
        {
            errors.Add("IP da catraca deve ser IPv4 válido.");
        }

        return errors.Count == 0
            ? new SetupValidationResult(true, errors)
            : new SetupValidationResult(false, errors);
    }

    public async Task SaveAsync(AgentSetupDto setup, CancellationToken cancellationToken = default)
    {
        var validation = Validate(setup);
        if (!validation.Success)
        {
            throw new InvalidOperationException(string.Join(" ", validation.Errors));
        }

        var configPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
        JsonObject root;

        if (File.Exists(configPath))
        {
            var existingJson = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            root = JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var orbis = root[OrbisOptions.SectionName] as JsonObject ?? new JsonObject();
        orbis[nameof(OrbisOptions.ApiBaseUrl)] = setup.ApiBaseUrl.Trim();
        orbis[nameof(OrbisOptions.ApiKey)] = setup.ApiKey.Trim();
        orbis[nameof(OrbisOptions.DeviceCode)] = setup.DeviceCode.Trim();
        root[OrbisOptions.SectionName] = orbis;

        var toletus = root[ToletusOptions.SectionName] as JsonObject ?? new JsonObject();
        toletus[nameof(ToletusOptions.Ip)] = setup.ToletusIp.Trim();
        toletus[nameof(ToletusOptions.SerialNumber)] = setup.ToletusSerialNumber?.Trim() ?? string.Empty;
        root[ToletusOptions.SectionName] = toletus;

        var agent = root[AgentOptions.SectionName] as JsonObject ?? new JsonObject();
        agent[nameof(AgentOptions.SetupComplete)] = true;
        root[AgentOptions.SectionName] = agent;

        var output = root.ToJsonString(JsonWriteOptions);
        await File.WriteAllTextAsync(configPath, output, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        if (_configuration is IConfigurationRoot configurationRoot)
        {
            configurationRoot.Reload();
        }
    }
}
