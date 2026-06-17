namespace Orbis.ToletusAgent.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int DebounceMs { get; set; } = 3000;

    public int PolicyCacheMinutes { get; set; } = 10;

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public string DefaultOfflineMode { get; set; } = "fail_closed";

    public string LogDirectory { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Orbis",
            "ToletusAgent",
            "logs");

    public string HealthFilePath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Orbis",
            "ToletusAgent",
            "health.json");

    public long LogFileSizeLimitBytes { get; set; } = 52_428_800;

    public bool StatusUiEnabled { get; set; } = true;

    public int StatusUiPort { get; set; } = 5080;

    public string StatusUiBindAddress { get; set; } = "127.0.0.1";

    public bool SetupComplete { get; set; }

    public string SetupAuthFilePath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Orbis",
            "ToletusAgent",
            "setup-auth.json");
}
