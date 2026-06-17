namespace Orbis.ToletusAgent.Toletus;

public sealed record ToletusAccessEvent(
    ToletusCredentialType CredentialType,
    string CredentialValue,
    DateTimeOffset Timestamp,
    object Raw);
