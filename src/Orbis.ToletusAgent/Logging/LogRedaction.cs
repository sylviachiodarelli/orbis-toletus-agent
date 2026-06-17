using System.Text.RegularExpressions;

namespace Orbis.ToletusAgent.Logging;

public static partial class LogRedaction
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var redacted = value;
        redacted = ApiKeyPattern().Replace(redacted, "***api-key***");
        redacted = CpfPattern().Replace(redacted, "***.***.***-**");
        return redacted;
    }

    [GeneratedRegex(@"\b\d{11}\b")]
    private static partial Regex CpfPattern();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex ApiKeyPattern();
}
