using System.Text.Json;

namespace Orbis.ToletusAgent.Orbis;

public static class AccessDecisionParser
{
    public static AccessDecision Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    public static AccessDecision Parse(JsonElement root)
    {
        var authorized = IsAuthorized(root);
        if (!authorized && root.TryGetProperty("v1", out var v1) && v1.ValueKind == JsonValueKind.Object)
        {
            authorized = IsAuthorized(v1);
        }

        return new AccessDecision(
            Authorized: authorized,
            Message: ReadString(root, "message", "mensagem", "motivo") ?? string.Empty,
            StudentName: ReadString(root, "student_name", "nome_aluno"),
            StatusPagamento: ReadString(root, "status_pagamento"),
            OfflineMode: ReadString(root, "offline_mode"));
    }

    private static bool IsAuthorized(JsonElement root)
    {
        if (ReadBool(root, "authorized")) return true;
        if (ReadBool(root, "autorizado")) return true;
        if (ReadBool(root, "permitido")) return true;
        if (ReadBool(root, "allow")) return true;
        if (ReadBool(root, "liberar")) return true;
        if (ReadBool(root, "liberado")) return true;
        if (ReadInt(root, "resultado") == 1) return true;

        return false;
    }

    private static string? ReadString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            else if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static bool ReadBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static int? ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
