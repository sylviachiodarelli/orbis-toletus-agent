using Toletus.LiteNet2.Command;
using Toletus.LiteNet2.Command.Enums;

namespace Orbis.ToletusAgent.Toletus;

internal static class ToletusAccessEventMapper
{
    public static ToletusAccessEvent? Map(Identification identification)
    {
        var credentialType = identification.Device switch
        {
            IdentificationDevice.Keyboard or IdentificationDevice.BarCode => ToletusCredentialType.EnrollId,
            IdentificationDevice.Rfid => ToletusCredentialType.Rfid,
            IdentificationDevice.EmbeddedFingerprint or IdentificationDevice.TemplateFingerprint
                => ToletusCredentialType.Fingerprint,
            _ => (ToletusCredentialType?)null
        };

        if (credentialType is null)
        {
            return null;
        }

        return new ToletusAccessEvent(
            credentialType.Value,
            identification.Id.ToString(),
            DateTimeOffset.UtcNow,
            new
            {
                identification.Device,
                identification.Data,
                identification.Id,
                RfidRaw = identification.RfidCard?.RawWeigand
            });
    }
}
