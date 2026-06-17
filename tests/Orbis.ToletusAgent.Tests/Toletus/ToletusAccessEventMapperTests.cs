using Orbis.ToletusAgent.Toletus;
using Toletus.LiteNet2.Command;
using Toletus.LiteNet2.Command.Enums;

namespace Orbis.ToletusAgent.Tests.Toletus;

public class ToletusAccessEventMapperTests
{
    [Theory]
    [InlineData(IdentificationDevice.Keyboard, ToletusCredentialType.EnrollId, "50", 50)]
    [InlineData(IdentificationDevice.BarCode, ToletusCredentialType.EnrollId, "12", 12)]
    [InlineData(IdentificationDevice.Rfid, ToletusCredentialType.Rfid, "74565", 0x12345)]
    [InlineData(IdentificationDevice.EmbeddedFingerprint, ToletusCredentialType.Fingerprint, "7", 7)]
    public void Map_translates_supported_identification_devices(
        IdentificationDevice device,
        ToletusCredentialType expectedType,
        string expectedValue,
        int data)
    {
        var identification = new Identification(device, data);

        var mapped = ToletusAccessEventMapper.Map(identification);

        Assert.NotNull(mapped);
        Assert.Equal(expectedType, mapped.CredentialType);
        Assert.Equal(expectedValue, mapped.CredentialValue);
    }
}
