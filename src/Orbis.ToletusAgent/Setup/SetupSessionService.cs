using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Orbis.ToletusAgent.Setup;

public sealed class SetupSessionService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private readonly SetupAuthStore _authStore;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _sessions = new();

    public SetupSessionService(SetupAuthStore authStore)
    {
        _authStore = authStore;
    }

    public string CreateSession()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = DateTimeOffset.UtcNow.Add(SessionLifetime);
        return token;
    }

    public bool IsValidSession(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!_sessions.TryGetValue(token, out var expiresAt))
        {
            return false;
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        _sessions[token] = DateTimeOffset.UtcNow.Add(SessionLifetime);
        return true;
    }

    public void RevokeSession(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _sessions.TryRemove(token, out _);
        }
    }

    public string SignSessionCookie(string token)
    {
        var secret = _authStore.GetOrCreateSessionSecret();
        var signature = ComputeHmac(token, secret);
        return $"{token}.{signature}";
    }

    public bool TryValidateSignedCookie(string? signedValue, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(signedValue))
        {
            return false;
        }

        var separator = signedValue.LastIndexOf('.');
        if (separator <= 0 || separator >= signedValue.Length - 1)
        {
            return false;
        }

        token = signedValue[..separator];
        var signature = signedValue[(separator + 1)..];
        var secret = _authStore.GetOrCreateSessionSecret();
        var expected = ComputeHmac(token, secret);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expected)))
        {
            token = string.Empty;
            return false;
        }

        if (!IsValidSession(token))
        {
            token = string.Empty;
            return false;
        }

        return true;
    }

    private static string ComputeHmac(string value, string secret) =>
        Convert.ToBase64String(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(value)));
}
