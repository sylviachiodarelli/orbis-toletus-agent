using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;

namespace Orbis.ToletusAgent.Setup;

public sealed class SetupAuthStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AgentOptions _agentOptions;
    private readonly object _sync = new();

    public SetupAuthStore(IOptions<AgentOptions> agentOptions)
    {
        _agentOptions = agentOptions.Value;
    }

    public bool HasPassword()
    {
        var state = LoadState();
        return !string.IsNullOrWhiteSpace(state.PasswordHash);
    }

    public bool VerifyPassword(string password)
    {
        var state = LoadState();
        return SetupPasswordHasher.VerifyPassword(password, state.PasswordHash);
    }

    public void CreatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
        }

        lock (_sync)
        {
            var state = LoadStateUnsafe();
            if (!string.IsNullOrWhiteSpace(state.PasswordHash))
            {
                throw new InvalidOperationException("Setup password is already configured.");
            }

            state.PasswordHash = SetupPasswordHasher.HashPassword(password);
            if (string.IsNullOrWhiteSpace(state.SessionSecret))
            {
                state.SessionSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            }

            SaveStateUnsafe(state);
        }
    }

    public string GetOrCreateSessionSecret()
    {
        lock (_sync)
        {
            var state = LoadStateUnsafe();
            if (string.IsNullOrWhiteSpace(state.SessionSecret))
            {
                state.SessionSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                SaveStateUnsafe(state);
            }

            return state.SessionSecret;
        }
    }

    private SetupAuthState LoadState()
    {
        lock (_sync)
        {
            return LoadStateUnsafe();
        }
    }

    private SetupAuthState LoadStateUnsafe()
    {
        var path = _agentOptions.SetupAuthFilePath;
        if (!File.Exists(path))
        {
            return new SetupAuthState();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SetupAuthState>(json) ?? new SetupAuthState();
        }
        catch (JsonException)
        {
            return new SetupAuthState();
        }
    }

    private void SaveStateUnsafe(SetupAuthState state)
    {
        var path = _agentOptions.SetupAuthFilePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    private sealed class SetupAuthState
    {
        public string PasswordHash { get; set; } = string.Empty;

        public string SessionSecret { get; set; } = string.Empty;
    }
}
