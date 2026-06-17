using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;

namespace Orbis.ToletusAgent.Core;

public interface IOfflinePolicyCache
{
    void UpdateFromDecision(string? offlineMode);

    bool ResolveOfflineAuthorized();

    string GetEffectiveOfflineMode();
}

public sealed class OfflinePolicyCache : IOfflinePolicyCache
{
    private readonly AgentOptions _agentOptions;
    private readonly TimeProvider _timeProvider;
    private readonly object _sync = new();
    private OfflineMode? _cachedMode;
    private DateTimeOffset? _cachedAt;

    public OfflinePolicyCache(IOptions<AgentOptions> agentOptions, TimeProvider timeProvider)
    {
        _agentOptions = agentOptions.Value;
        _timeProvider = timeProvider;
    }

    public void UpdateFromDecision(string? offlineMode)
    {
        if (!TryParseOfflineMode(offlineMode, out var parsed))
        {
            return;
        }

        lock (_sync)
        {
            _cachedMode = parsed;
            _cachedAt = _timeProvider.GetUtcNow();
        }
    }

    public bool ResolveOfflineAuthorized()
    {
        var mode = GetEffectiveMode();
        return mode == OfflineMode.FailOpen;
    }

    public string GetEffectiveOfflineMode()
    {
        return GetEffectiveMode() == OfflineMode.FailOpen ? "fail_open" : "fail_closed";
    }

    private OfflineMode GetEffectiveMode()
    {
        lock (_sync)
        {
            if (_cachedMode is not null && _cachedAt is not null)
            {
                var ttl = TimeSpan.FromMinutes(_agentOptions.PolicyCacheMinutes);
                if (_timeProvider.GetUtcNow() - _cachedAt.Value <= ttl)
                {
                    return _cachedMode.Value;
                }
            }
        }

        return ParseDefaultOfflineMode(_agentOptions.DefaultOfflineMode);
    }

    private static bool TryParseOfflineMode(string? value, out OfflineMode mode)
    {
        if (string.Equals(value, "fail_open", StringComparison.OrdinalIgnoreCase))
        {
            mode = OfflineMode.FailOpen;
            return true;
        }

        if (string.Equals(value, "fail_closed", StringComparison.OrdinalIgnoreCase))
        {
            mode = OfflineMode.FailClosed;
            return true;
        }

        mode = default;
        return false;
    }

    private static OfflineMode ParseDefaultOfflineMode(string value)
    {
        return string.Equals(value, "fail_open", StringComparison.OrdinalIgnoreCase)
            ? OfflineMode.FailOpen
            : OfflineMode.FailClosed;
    }
}
