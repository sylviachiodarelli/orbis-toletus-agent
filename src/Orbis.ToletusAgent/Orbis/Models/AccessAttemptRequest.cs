namespace Orbis.ToletusAgent.Orbis;

public sealed record AccessAttemptRequest(
    string TransactionId,
    string Direction,
    string DeviceCode,
    string CredentialType,
    string CredentialValue,
    object? RawPayload);
