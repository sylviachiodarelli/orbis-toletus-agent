namespace Orbis.ToletusAgent.Health;

public sealed class AgentRecoveryState
{
    private readonly object _sync = new();

    public DateTimeOffset? SdkDisconnectedSince { get; private set; }

    public int ConsecutiveReconnectAttempts { get; private set; }

    public DateTimeOffset? LastReconnectAt { get; private set; }

    public string? LastRecoveryMessage { get; private set; }

    public DateTimeOffset? LastRecoveryAt { get; private set; }

    public void MarkSdkConnected()
    {
        lock (_sync)
        {
            SdkDisconnectedSince = null;
            ConsecutiveReconnectAttempts = 0;
        }
    }

    public void MarkSdkDisconnected()
    {
        lock (_sync)
        {
            SdkDisconnectedSince ??= DateTimeOffset.UtcNow;
        }
    }

    public void RecordReconnectAttempt(bool success, string message)
    {
        lock (_sync)
        {
            LastReconnectAt = DateTimeOffset.UtcNow;
            LastRecoveryAt = LastReconnectAt;
            LastRecoveryMessage = message;
            if (success)
            {
                SdkDisconnectedSince = null;
                ConsecutiveReconnectAttempts = 0;
                return;
            }

            ConsecutiveReconnectAttempts++;
        }
    }

    public void RecordRecoveryMessage(string message)
    {
        lock (_sync)
        {
            LastRecoveryAt = DateTimeOffset.UtcNow;
            LastRecoveryMessage = message;
        }
    }

    public (DateTimeOffset? DisconnectedSince, int ReconnectAttempts) Snapshot()
    {
        lock (_sync)
        {
            return (SdkDisconnectedSince, ConsecutiveReconnectAttempts);
        }
    }
}
