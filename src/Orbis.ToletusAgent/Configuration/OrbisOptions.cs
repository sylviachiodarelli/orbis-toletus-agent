namespace Orbis.ToletusAgent.Configuration;

public sealed class OrbisOptions
{
    public const string SectionName = "Orbis";

    public string ApiBaseUrl { get; set; } = "https://orbisfit.com";

    public string ApiKey { get; set; } = string.Empty;

    public string DeviceCode { get; set; } = string.Empty;

    public string AccessAttemptPath { get; set; } = "/api/turnstile/access-attempt";

    public string LegacyValidationPath { get; set; } = "/api/verificar-acesso-catraca";

    public bool UseV2Endpoint { get; set; } = true;

    public int TimeoutMs { get; set; } = 5000;

    public int MaxRetries { get; set; } = 2;

    public int[] RetryBackoffMs { get; set; } = [500, 1500];
}
