namespace Orbis.ToletusAgent.Health;

public sealed record RecoveryActionResult(bool Success, string Message, bool SdkConnected = false);

public interface IAgentRecoveryService
{
    Task<RecoveryActionResult> ReconnectTurnstileAsync(CancellationToken cancellationToken = default);

    RecoveryActionResult ScheduleApplicationRestart();
}
