using Orbis.ToletusAgent.Orbis;

namespace Orbis.ToletusAgent.Tests.Orbis;

public class AccessDecisionParserTests
{
    [Fact]
    public void Parse_v2_response_uses_authorized_field()
    {
        const string json = """
            {
              "ok": true,
              "authorized": true,
              "message": "Acesso liberado",
              "status_pagamento": "em_dia",
              "student_name": "João Silva",
              "offline_mode": "fail_closed"
            }
            """;

        var decision = AccessDecisionParser.Parse(json);

        Assert.True(decision.Authorized);
        Assert.Equal("Acesso liberado", decision.Message);
        Assert.Equal("João Silva", decision.StudentName);
        Assert.Equal("em_dia", decision.StatusPagamento);
        Assert.Equal("fail_closed", decision.OfflineMode);
    }

    [Fact]
    public void Parse_v1_response_uses_legacy_fields()
    {
        const string json = """
            {
              "autorizado": false,
              "permitido": false,
              "resultado": 0,
              "mensagem": "Inadimplente",
              "nome_aluno": "Maria",
              "status_pagamento": "inadimplente"
            }
            """;

        var decision = AccessDecisionParser.Parse(json);

        Assert.False(decision.Authorized);
        Assert.Equal("Inadimplente", decision.Message);
        Assert.Equal("Maria", decision.StudentName);
        Assert.Equal("inadimplente", decision.StatusPagamento);
    }

    [Theory]
    [InlineData("""{ "resultado": 1, "motivo": "OK" }""", true)]
    [InlineData("""{ "permitido": true, "motivo": "OK" }""", true)]
    [InlineData("""{ "authorized": false, "v1": { "autorizado": true, "mensagem": "fallback" } }""", true)]
    public void Parse_supports_compatibility_fields(string json, bool expectedAuthorized)
    {
        var decision = AccessDecisionParser.Parse(json);

        Assert.Equal(expectedAuthorized, decision.Authorized);
    }
}
