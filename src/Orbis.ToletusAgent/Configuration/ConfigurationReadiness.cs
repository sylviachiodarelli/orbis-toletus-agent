namespace Orbis.ToletusAgent.Configuration;

public static class ConfigurationReadiness
{
    public static bool IsSetupComplete(IConfiguration configuration) =>
        configuration.GetValue<bool>($"{AgentOptions.SectionName}:{nameof(AgentOptions.SetupComplete)}");

    public static bool HasMinimumRuntimeConfig(IConfiguration configuration)
    {
        var orbis = configuration.GetSection(OrbisOptions.SectionName);
        var toletus = configuration.GetSection(ToletusOptions.SectionName);

        return !string.IsNullOrWhiteSpace(orbis[nameof(OrbisOptions.ApiKey)])
            && !string.IsNullOrWhiteSpace(orbis[nameof(OrbisOptions.DeviceCode)])
            && !string.IsNullOrWhiteSpace(toletus[nameof(ToletusOptions.Ip)]);
    }
}
