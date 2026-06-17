namespace Orbis.ToletusAgent.Status;

public sealed record AccessActivityEntry(
    DateTimeOffset Timestamp,
    string TransactionId,
    string CredentialType,
    string CredentialValueMasked,
    bool? Authorized,
    string Outcome,
    string? Message,
    string? StudentName);

public interface IAgentActivityStore
{
    void Record(AccessActivityEntry entry);

    IReadOnlyList<AccessActivityEntry> GetRecent(int count = 50);
}

public sealed class AgentActivityStore : IAgentActivityStore
{
    private const int MaxEntries = 100;
    private readonly object _sync = new();
    private readonly LinkedList<AccessActivityEntry> _entries = new();

    public void Record(AccessActivityEntry entry)
    {
        lock (_sync)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveLast();
            }
        }
    }

    public IReadOnlyList<AccessActivityEntry> GetRecent(int count = 50)
    {
        lock (_sync)
        {
            return _entries.Take(count).ToList();
        }
    }
}
