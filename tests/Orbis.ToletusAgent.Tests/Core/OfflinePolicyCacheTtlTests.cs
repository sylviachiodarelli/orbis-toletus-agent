using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Core;

namespace Orbis.ToletusAgent.Tests.Core;

public class OfflinePolicyCacheTtlTests
{
    [Fact]
    public void ResolveOfflineAuthorized_falls_back_after_cache_ttl_expires()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));
        var cache = new OfflinePolicyCache(
            Options.Create(new AgentOptions
            {
                DefaultOfflineMode = "fail_closed",
                PolicyCacheMinutes = 10
            }),
            clock);

        cache.UpdateFromDecision("fail_open");
        Assert.True(cache.ResolveOfflineAuthorized());

        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.False(cache.ResolveOfflineAuthorized());
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
