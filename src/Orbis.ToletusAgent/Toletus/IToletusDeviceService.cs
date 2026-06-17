namespace Orbis.ToletusAgent.Toletus;

public interface IToletusDeviceService
{
    event EventHandler<ToletusAccessEvent>? AccessAttemptReceived;

    bool IsConnected { get; }

    string? FirmwareVersion { get; }

    string? SerialNumber { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task ReleaseTurnstileAsync(string? message = null, CancellationToken cancellationToken = default);

    Task DenyTurnstileAsync(string? message = null, CancellationToken cancellationToken = default);
}
