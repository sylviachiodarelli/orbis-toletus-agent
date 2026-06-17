using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Core;

namespace Orbis.ToletusAgent.Tests.Core;

public class OfflinePolicyCacheTests
{
    [Fact]
    public void ResolveOfflineAuthorized_uses_fail_closed_default_without_cache()
    {
        var cache = CreateCache("fail_closed");

        Assert.False(cache.ResolveOfflineAuthorized());
    }

    [Fact]
    public void ResolveOfflineAuthorized_uses_fail_open_default_when_configured()
    {
        var cache = CreateCache("fail_open");

        Assert.True(cache.ResolveOfflineAuthorized());
    }

    [Fact]
    public void UpdateFromDecision_uses_cached_fail_open_when_ttl_valid()
    {
        var cache = CreateCache("fail_closed");
        cache.UpdateFromDecision("fail_open");

        Assert.True(cache.ResolveOfflineAuthorized());
    }

    [Fact]
    public void UpdateFromDecision_ignores_unknown_values()
    {
        var cache = CreateCache("fail_closed");
        cache.UpdateFromDecision("invalid");

        Assert.False(cache.ResolveOfflineAuthorized());
    }

    private static OfflinePolicyCache CreateCache(string defaultOfflineMode)
    {
        return new OfflinePolicyCache(
            Options.Create(new AgentOptions
            {
                DefaultOfflineMode = defaultOfflineMode,
                PolicyCacheMinutes = 10
            }),
            TimeProvider.System);
    }
}
