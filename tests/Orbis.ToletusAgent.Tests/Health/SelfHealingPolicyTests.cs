using Orbis.ToletusAgent.Health;

namespace Orbis.ToletusAgent.Tests.Health;

public class SelfHealingPolicyTests
{
    [Fact]
    public void ShouldForceReconnect_after_threshold()
    {
        var should = SelfHealingPolicy.ShouldForceReconnect(
            TimeSpan.FromSeconds(120),
            forceReconnectAfterSeconds: 90,
            enabled: true);

        Assert.True(should);
    }

    [Fact]
    public void ShouldForceReconnect_false_when_disabled()
    {
        var should = SelfHealingPolicy.ShouldForceReconnect(
            TimeSpan.FromMinutes(10),
            forceReconnectAfterSeconds: 90,
            enabled: false);

        Assert.False(should);
    }

    [Fact]
    public void ShouldRestartApplication_after_max_attempts()
    {
        var should = SelfHealingPolicy.ShouldRestartApplication(
            TimeSpan.FromMinutes(2),
            consecutiveReconnectAttempts: 8,
            maxConsecutiveReconnectAttempts: 8,
            applicationRestartAfterSeconds: 900,
            enabled: true);

        Assert.True(should);
    }

    [Fact]
    public void ShouldRestartApplication_after_long_disconnect()
    {
        var should = SelfHealingPolicy.ShouldRestartApplication(
            TimeSpan.FromMinutes(20),
            consecutiveReconnectAttempts: 1,
            maxConsecutiveReconnectAttempts: 8,
            applicationRestartAfterSeconds: 900,
            enabled: true);

        Assert.True(should);
    }
}
