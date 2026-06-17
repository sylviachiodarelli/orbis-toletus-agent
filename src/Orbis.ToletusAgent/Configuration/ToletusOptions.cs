namespace Orbis.ToletusAgent.Configuration;

public sealed class ToletusOptions
{
    public const string SectionName = "Toletus";

    public string Ip { get; set; } = string.Empty;

    public int Port { get; set; }

    public string SerialNumber { get; set; } = string.Empty;
}
