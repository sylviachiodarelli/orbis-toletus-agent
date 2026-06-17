namespace Orbis.ToletusAgent.Orbis;

public sealed record AccessDecision(
    bool Authorized,
    string Message,
    string? StudentName,
    string? StatusPagamento,
    string? OfflineMode);
