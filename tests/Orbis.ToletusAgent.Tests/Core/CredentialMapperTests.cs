using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Core;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Tests.Core;

public class CredentialMapperTests
{
    private static readonly OrbisOptions OrbisOptions = new()
    {
        DeviceCode = "CATRACA-01"
    };

    private static readonly ToletusOptions ToletusOptions = new()
    {
        Ip = "192.168.0.220",
        SerialNumber = "7218"
    };

    [Theory]
    [InlineData(ToletusCredentialType.EnrollId, "50", "ENROLLID", "50")]
    [InlineData(ToletusCredentialType.EnrollId, "007", "ENROLLID", "007")]
    [InlineData(ToletusCredentialType.Fingerprint, "12", "FINGERPRINT", "12")]
    [InlineData(ToletusCredentialType.Rfid, "74565", "RFID", "74565")]
    [InlineData(ToletusCredentialType.EnrollId, "12345678901", "CPF", "12345678901")]
    public void MapCredential_translates_supported_types(
        ToletusCredentialType inputType,
        string inputValue,
        string expectedType,
        string expectedValue)
    {
        var accessEvent = new ToletusAccessEvent(inputType, inputValue, DateTimeOffset.UtcNow, new { });

        var (credentialType, credentialValue) = CredentialMapper.MapCredential(accessEvent);

        Assert.Equal(expectedType, credentialType);
        Assert.Equal(expectedValue, credentialValue);
    }

    [Fact]
    public void Map_builds_transaction_id_with_serial_and_timestamp()
    {
        var accessEvent = new ToletusAccessEvent(
            ToletusCredentialType.EnrollId,
            "50",
            DateTimeOffset.UtcNow,
            new { });

        var request = CredentialMapper.Map(accessEvent, OrbisOptions, ToletusOptions, "V2.2.2 R0");

        Assert.StartsWith("toletus-7218-", request.TransactionId, StringComparison.Ordinal);
        Assert.Equal("IN", request.Direction);
        Assert.Equal("CATRACA-01", request.DeviceCode);
        Assert.Equal("ENROLLID", request.CredentialType);
    }

    [Fact]
    public void MapCredential_returns_empty_enrollid_for_blank_value()
    {
        var accessEvent = new ToletusAccessEvent(
            ToletusCredentialType.EnrollId,
            "   ",
            DateTimeOffset.UtcNow,
            new { });

        var (credentialType, credentialValue) = CredentialMapper.MapCredential(accessEvent);

        Assert.Equal("ENROLLID", credentialType);
        Assert.Equal(string.Empty, credentialValue);
    }

    [Fact]
    public void MaskCredentialForLog_masks_cpf()
    {
        var masked = CredentialMapper.MaskCredentialForLog("CPF", "12345678901");

        Assert.Equal("***.***.***-**", masked);
    }
}
