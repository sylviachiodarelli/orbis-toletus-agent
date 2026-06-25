namespace Orbis.ToletusAgent.Health;

public static class SelfHealingPolicy
{
    public static bool ShouldForceReconnect(
        TimeSpan disconnectedFor,
        int forceReconnectAfterSeconds,
        bool enabled)
    {
        if (!enabled || forceReconnectAfterSeconds <= 0)
        {
            return false;
        }

        return disconnectedFor >= TimeSpan.FromSeconds(forceReconnectAfterSeconds);
    }

    public static bool ShouldRestartApplication(
        TimeSpan disconnectedFor,
        int consecutiveReconnectAttempts,
        int maxConsecutiveReconnectAttempts,
        int applicationRestartAfterSeconds,
        bool enabled)
    {
        if (!enabled)
        {
            return false;
        }

        if (maxConsecutiveReconnectAttempts > 0
            && consecutiveReconnectAttempts >= maxConsecutiveReconnectAttempts)
        {
            return true;
        }

        if (applicationRestartAfterSeconds > 0
            && disconnectedFor >= TimeSpan.FromSeconds(applicationRestartAfterSeconds))
        {
            return true;
        }

        return false;
    }
}
