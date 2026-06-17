namespace Orbis.ToletusAgent.Configuration;

internal static class EnvironmentVariableMapper
{
    public static void Apply(IConfigurationBuilder configurationBuilder)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        Map(overrides, "ORBIS_API_BASE_URL", $"{OrbisOptions.SectionName}:ApiBaseUrl");
        Map(overrides, "ORBIS_API_KEY", $"{OrbisOptions.SectionName}:ApiKey");
        Map(overrides, "ORBIS_DEVICE_CODE", $"{OrbisOptions.SectionName}:DeviceCode");
        Map(overrides, "TOLETUS_IP", $"{ToletusOptions.SectionName}:Ip");
        Map(overrides, "TOLETUS_SERIAL", $"{ToletusOptions.SectionName}:SerialNumber");

        if (overrides.Count > 0)
        {
            configurationBuilder.AddInMemoryCollection(overrides);
        }
    }

    private static void Map(Dictionary<string, string?> overrides, string envVar, string configKey)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(value))
        {
            overrides[configKey] = value;
        }
    }
}
