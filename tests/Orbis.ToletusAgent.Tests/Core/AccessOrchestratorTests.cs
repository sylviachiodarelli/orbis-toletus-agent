using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Core;
using Orbis.ToletusAgent.Health;
using Orbis.ToletusAgent.Orbis;
using Orbis.ToletusAgent.Status;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Tests.Core;

public class AccessOrchestratorTests
{
    [Fact]
    public async Task ProcessAccessAttemptAsync_releases_turnstile_when_authorized()
    {
        var device = new FakeToletusDeviceService();
        var orbis = new FakeOrbisApiClient(_ => new AccessDecision(true, "Acesso liberado", "João", "em_dia", "fail_closed"));
        var cache = new OfflinePolicyCache(Options.Create(new AgentOptions()), TimeProvider.System);
        var orchestrator = CreateOrchestrator(device, orbis, cache);

        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));

        Assert.True(device.Released);
        Assert.False(device.Denied);
        Assert.Single(orbis.Requests);
    }

    [Fact]
    public async Task ProcessAccessAttemptAsync_denies_turnstile_when_not_authorized()
    {
        var device = new FakeToletusDeviceService();
        var orbis = new FakeOrbisApiClient(_ => new AccessDecision(false, "Inadimplente", "Maria", "inadimplente", "fail_closed"));
        var cache = new OfflinePolicyCache(Options.Create(new AgentOptions()), TimeProvider.System);
        var orchestrator = CreateOrchestrator(device, orbis, cache);

        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));

        Assert.False(device.Released);
        Assert.True(device.Denied);
    }

    [Fact]
    public async Task ProcessAccessAttemptAsync_debounces_duplicate_reads()
    {
        var device = new FakeToletusDeviceService();
        var orbis = new FakeOrbisApiClient(_ => new AccessDecision(true, "ok", null, null, null));
        var cache = new OfflinePolicyCache(Options.Create(new AgentOptions { DebounceMs = 3000 }), TimeProvider.System);
        var orchestrator = CreateOrchestrator(device, orbis, cache);

        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));
        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));

        Assert.Single(orbis.Requests);
    }

    [Fact]
    public async Task ProcessAccessAttemptAsync_applies_fail_closed_when_api_unavailable()
    {
        var device = new FakeToletusDeviceService();
        var orbis = new FakeOrbisApiClient(_ => throw new OrbisApiException("server down", 503));
        var cache = new OfflinePolicyCache(Options.Create(new AgentOptions { DefaultOfflineMode = "fail_closed" }), TimeProvider.System);
        var orchestrator = CreateOrchestrator(device, orbis, cache);

        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));

        Assert.True(device.Denied);
        Assert.False(device.Released);
    }

    [Fact]
    public async Task ProcessAccessAttemptAsync_applies_cached_fail_open_when_api_unavailable()
    {
        var device = new FakeToletusDeviceService();
        var orbis = new FakeOrbisApiClient(_ => throw new OrbisApiException("server down", 503));
        var cache = new OfflinePolicyCache(Options.Create(new AgentOptions { DefaultOfflineMode = "fail_closed" }), TimeProvider.System);
        cache.UpdateFromDecision("fail_open");
        var orchestrator = CreateOrchestrator(device, orbis, cache);

        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));

        Assert.True(device.Released);
        Assert.False(device.Denied);
    }

    private static AccessOrchestrator CreateOrchestrator(
        FakeToletusDeviceService device,
        FakeOrbisApiClient orbis,
        OfflinePolicyCache cache)
    {
        return new AccessOrchestrator(
            NullLogger<AccessOrchestrator>.Instance,
            device,
            orbis,
            cache,
            new AgentHealthState(),
            new AgentActivityStore(),
            new TestHostApplicationLifetime(),
            Options.Create(new OrbisOptions { DeviceCode = "CATRACA-01" }),
            Options.Create(new ToletusOptions { SerialNumber = "7218" }),
            Options.Create(new AgentOptions { DebounceMs = 3000 }));
    }

    private static ToletusAccessEvent CreateEvent(string enrollId) =>
        new(ToletusCredentialType.EnrollId, enrollId, DateTimeOffset.UtcNow, new { });

    private sealed class FakeToletusDeviceService : IToletusDeviceService
    {
        public event EventHandler<ToletusAccessEvent>? AccessAttemptReceived;

        public bool IsConnected => true;

        public string? FirmwareVersion => "V2.2.2 R0";

        public string? SerialNumber => "7218";

        public bool Released { get; private set; }

        public bool Denied { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReleaseTurnstileAsync(string? message = null, CancellationToken cancellationToken = default)
        {
            Released = true;
            return Task.CompletedTask;
        }

        public Task DenyTurnstileAsync(string? message = null, CancellationToken cancellationToken = default)
        {
            Denied = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrbisApiClient : IOrbisApiClient
    {
        private readonly Func<AccessAttemptRequest, AccessDecision> _handler;

        public FakeOrbisApiClient(Func<AccessAttemptRequest, AccessDecision> handler)
        {
            _handler = handler;
        }

        public List<AccessAttemptRequest> Requests { get; } = [];

        public Task<AccessDecision> ValidateAccessAsync(
            AccessAttemptRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted { get; set; } = CancellationToken.None;

        public CancellationToken ApplicationStopping { get; set; } = CancellationToken.None;

        public CancellationToken ApplicationStopped { get; set; } = CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
