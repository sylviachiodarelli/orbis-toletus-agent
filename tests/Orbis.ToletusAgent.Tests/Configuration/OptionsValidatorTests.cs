using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;

namespace Orbis.ToletusAgent.Tests.Configuration;

public class AgentOptionsValidatorTests
{
    private readonly AgentOptionsValidator _validator = new();

    [Theory]
    [InlineData("fail_closed")]
    [InlineData("fail_open")]
    [InlineData("FAIL_CLOSED")]
    public void Validate_accepts_known_offline_modes(string mode)
    {
        var options = new AgentOptions { DefaultOfflineMode = mode };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_rejects_unknown_offline_mode()
    {
        var options = new AgentOptions { DefaultOfflineMode = "invalid" };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }
}

public class ToletusOptionsValidatorTests
{
    [Fact]
    public void Validate_accepts_empty_ip_before_setup_is_complete()
    {
        var configuration = new ConfigurationBuilder().Build();
        var validator = new ToletusOptionsValidator(configuration);
        var options = new ToletusOptions { Ip = string.Empty };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_accepts_valid_ipv4()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agent:SetupComplete"] = "true" })
            .Build();
        var validator = new ToletusOptionsValidator(configuration);
        var options = new ToletusOptions { Ip = "192.168.0.220" };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_rejects_invalid_ip()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agent:SetupComplete"] = "true" })
            .Build();
        var validator = new ToletusOptionsValidator(configuration);
        var options = new ToletusOptions { Ip = "not-an-ip" };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }
}

public class OrbisOptionsValidatorTests
{
    [Fact]
    public void Validate_requires_api_key_in_production_when_setup_complete()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agent:SetupComplete"] = "true" })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new OrbisOptionsValidator(environment, configuration);
        var options = new OrbisOptions
        {
            ApiBaseUrl = "https://orbisfit.com",
            ApiKey = "",
            DeviceCode = "CATRACA-01"
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_allows_empty_api_key_in_development()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agent:SetupComplete"] = "true" })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var validator = new OrbisOptionsValidator(environment, configuration);
        var options = new OrbisOptions
        {
            ApiBaseUrl = "https://orbisfit.com",
            ApiKey = "",
            DeviceCode = "CATRACA-01"
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}

file sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "Orbis.ToletusAgent.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } =
        new PhysicalFileProvider(AppContext.BaseDirectory);
}
