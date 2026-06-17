using Orbis.ToletusAgent.Logging;

namespace Orbis.ToletusAgent.Tests.Logging;

public class LogRedactionTests
{
    [Fact]
    public void Redact_masks_api_key_and_cpf()
    {
        var input = "key c056a52f-2b12-415d-b2e4-bf19408f80f5 cpf 12345678901";

        var redacted = LogRedaction.Redact(input);

        Assert.DoesNotContain("c056a52f", redacted);
        Assert.DoesNotContain("12345678901", redacted);
        Assert.Contains("***api-key***", redacted);
        Assert.Contains("***.***.***-**", redacted);
    }
}
