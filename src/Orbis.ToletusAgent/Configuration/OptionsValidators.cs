using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Orbis.ToletusAgent.Configuration;

public sealed class OrbisOptionsValidator : IValidateOptions<OrbisOptions>
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public OrbisOptionsValidator(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, OrbisOptions options)
    {
        if (!ConfigurationReadiness.IsSetupComplete(_configuration))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(_environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp)))
        {
            failures.Add("Orbis.ApiBaseUrl must be a valid absolute URL (HTTPS, or HTTP in Development).");
        }

        if (_environment.IsProduction() && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("Orbis.ApiKey is required in Production.");
        }

        if (string.IsNullOrWhiteSpace(options.DeviceCode))
        {
            failures.Add("Orbis.DeviceCode is required.");
        }

        if (options.TimeoutMs <= 0)
        {
            failures.Add("Orbis.TimeoutMs must be greater than zero.");
        }

        if (options.MaxRetries < 0)
        {
            failures.Add("Orbis.MaxRetries cannot be negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class ToletusOptionsValidator : IValidateOptions<ToletusOptions>
{
    private readonly IConfiguration _configuration;

    public ToletusOptionsValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, ToletusOptions options)
    {
        if (!ConfigurationReadiness.IsSetupComplete(_configuration))
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.Ip))
        {
            return ValidateOptionsResult.Fail("Toletus.Ip is required after setup is complete.");
        }

        if (!IPAddress.TryParse(options.Ip, out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return ValidateOptionsResult.Fail("Toletus.Ip must be a valid IPv4 address.");
        }

        if (options.Port is < 0 or > 65535)
        {
            return ValidateOptionsResult.Fail("Toletus.Port must be between 0 and 65535.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    private static readonly HashSet<string> AllowedOfflineModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "fail_closed",
        "fail_open"
    };

    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var failures = new List<string>();

        if (options.DebounceMs < 0)
        {
            failures.Add("Agent.DebounceMs cannot be negative.");
        }

        if (options.PolicyCacheMinutes <= 0)
        {
            failures.Add("Agent.PolicyCacheMinutes must be greater than zero.");
        }

        if (options.HeartbeatIntervalSeconds <= 0)
        {
            failures.Add("Agent.HeartbeatIntervalSeconds must be greater than zero.");
        }

        if (!AllowedOfflineModes.Contains(options.DefaultOfflineMode))
        {
            failures.Add("Agent.DefaultOfflineMode must be 'fail_closed' or 'fail_open'.");
        }

        if (options.StatusUiPort is < 1 or > 65535)
        {
            failures.Add("Agent.StatusUiPort must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.StatusUiBindAddress))
        {
            failures.Add("Agent.StatusUiBindAddress is required.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
