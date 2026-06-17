namespace Orbis.ToletusAgent.Health;

public interface IAgentHealthState
{
    DateTimeOffset? LastSuccessfulValidationAt { get; }

    void RecordSuccessfulValidation(DateTimeOffset timestamp);
}

public sealed class AgentHealthState : IAgentHealthState
{
    private DateTimeOffset? _lastSuccessfulValidationAt;

    public DateTimeOffset? LastSuccessfulValidationAt => _lastSuccessfulValidationAt;

    public void RecordSuccessfulValidation(DateTimeOffset timestamp)
    {
        _lastSuccessfulValidationAt = timestamp;
    }
}
