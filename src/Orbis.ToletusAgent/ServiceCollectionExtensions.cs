using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Core;
using Orbis.ToletusAgent.Health;
using Orbis.ToletusAgent.Orbis;
using Orbis.ToletusAgent.Status;
using Orbis.ToletusAgent.Setup;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<OrbisOptions>()
            .Bind(configuration.GetSection(OrbisOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<ToletusOptions>()
            .Bind(configuration.GetSection(ToletusOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<OrbisOptions>, OrbisOptionsValidator>();
        services.AddSingleton<IValidateOptions<ToletusOptions>, ToletusOptionsValidator>();
        services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();

        services.AddHttpClient<IOrbisApiClient, OrbisApiClient>((_, client) =>
        {
            var apiBaseUrl = configuration.GetSection(OrbisOptions.SectionName).GetValue<string>(nameof(OrbisOptions.ApiBaseUrl))
                ?? "https://orbisfit.com";
            client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        });
        services.AddHttpClient();
        services.AddSingleton<SetupAuthStore>();
        services.AddSingleton<SetupSessionService>();
        services.AddSingleton<IAgentConfigurationService, AgentConfigurationService>();
        services.AddSingleton<SetupConnectionTester>();
        services.AddHostedService<SetupBootstrapHostedService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IToletusDeviceService, ToletusDeviceService>();
        services.AddSingleton<IAgentHealthState, AgentHealthState>();
        services.AddSingleton<IAgentActivityStore, AgentActivityStore>();
        services.AddSingleton<IOfflinePolicyCache, OfflinePolicyCache>();
        services.AddSingleton<AccessOrchestrator>();
        services.AddSingleton<IAccessOrchestrator>(sp => sp.GetRequiredService<AccessOrchestrator>());
        services.AddHostedService(sp => sp.GetRequiredService<AccessOrchestrator>());
        services.AddHostedService<AgentHealthService>();
        services.AddHostedService<HostLifetimeLogger>();
        services.AddHostedService<AgentHostedService>();
        services.AddHostedService<ToletusConnectionHostedService>();

        return services;
    }
}
