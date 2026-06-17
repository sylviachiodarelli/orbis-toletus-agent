using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Orbis;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Core;

public static partial class CredentialMapper
{
    private const string DefaultDirection = "IN";

    public static AccessAttemptRequest Map(
        ToletusAccessEvent accessEvent,
        OrbisOptions orbisOptions,
        ToletusOptions toletusOptions,
        string? firmwareVersion)
    {
        var (credentialType, credentialValue) = MapCredential(accessEvent);
        var serial = string.IsNullOrWhiteSpace(toletusOptions.SerialNumber)
            ? "unknown"
            : toletusOptions.SerialNumber;

        return new AccessAttemptRequest(
            TransactionId: BuildTransactionId(serial),
            Direction: DefaultDirection,
            DeviceCode: orbisOptions.DeviceCode,
            CredentialType: credentialType,
            CredentialValue: credentialValue,
            RawPayload: new
            {
                vendor = "toletus",
                firmware = firmwareVersion,
                serial = toletusOptions.SerialNumber,
                agent_version = GetAgentVersion()
            });
    }

    public static (string CredentialType, string CredentialValue) MapCredential(ToletusAccessEvent accessEvent)
    {
        var value = accessEvent.CredentialValue?.Trim() ?? string.Empty;

        return accessEvent.CredentialType switch
        {
            ToletusCredentialType.EnrollId => MapEnrollIdOrCpf(value),
            ToletusCredentialType.Fingerprint => ("FINGERPRINT", value),
            ToletusCredentialType.Rfid => ("RFID", value),
            _ => throw new ArgumentOutOfRangeException(nameof(accessEvent), accessEvent.CredentialType, "Unsupported credential type.")
        };
    }

    public static string MaskCredentialForLog(string credentialType, string credentialValue)
    {
        if (string.Equals(credentialType, "CPF", StringComparison.OrdinalIgnoreCase))
        {
            return "***.***.***-**";
        }

        return credentialValue;
    }

    private static (string CredentialType, string CredentialValue) MapEnrollIdOrCpf(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ("ENROLLID", string.Empty);
        }

        var digits = DigitsOnly(value);
        if (digits.Length == 11 && CpfPattern().IsMatch(digits))
        {
            return ("CPF", digits);
        }

        return ("ENROLLID", value);
    }

    private static string BuildTransactionId(string serial)
    {
        return $"toletus-{serial}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static string GetAgentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex CpfPattern();
}
