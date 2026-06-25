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

public class DebounceTests
{
    [Fact]
    public async Task Duplicate_reads_within_debounce_window_trigger_single_api_call()
    {
        var device = new FakeToletusDeviceService();
        var orbis = new FakeOrbisApiClient(_ => new AccessDecision(true, "ok", null, null, null));
        var orchestrator = CreateOrchestrator(device, orbis, debounceMs: 5000);

        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));
        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));

        Assert.Single(orbis.Requests);
    }

    [Fact]
    public async Task Different_credentials_are_not_debounced()
    {
        var device = new FakeToletusDeviceService();
        var orbis = new FakeOrbisApiClient(_ => new AccessDecision(true, "ok", null, null, null));
        var orchestrator = CreateOrchestrator(device, orbis, debounceMs: 5000);

        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("50"));
        await orchestrator.ProcessAccessAttemptAsync(CreateEvent("51"));

        Assert.Equal(2, orbis.Requests.Count);
    }

    private static AccessOrchestrator CreateOrchestrator(
        FakeToletusDeviceService device,
        FakeOrbisApiClient orbis,
        int debounceMs)
    {
        return new AccessOrchestrator(
            NullLogger<AccessOrchestrator>.Instance,
            device,
            orbis,
            new OfflinePolicyCache(
                Options.Create(new AgentOptions { DebounceMs = debounceMs }),
                TimeProvider.System),
            new AgentHealthState(),
            new AgentActivityStore(),
            new TestHostApplicationLifetime(),
            Options.Create(new OrbisOptions { DeviceCode = "CATRACA-01" }),
            Options.Create(new ToletusOptions { SerialNumber = "7218" }),
            Options.Create(new AgentOptions { DebounceMs = debounceMs }));
    }

    private static ToletusAccessEvent CreateEvent(string enrollId) =>
        new(ToletusCredentialType.EnrollId, enrollId, DateTimeOffset.UtcNow, new { });

    private sealed class FakeToletusDeviceService : IToletusDeviceService
    {
#pragma warning disable CS0067
        public event EventHandler<ToletusAccessEvent>? AccessAttemptReceived;
#pragma warning restore CS0067

        public bool IsConnected => true;

        public string? FirmwareVersion => "V2.2.2 R0";

        public string? SerialNumber => "7218";

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ForceReconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReleaseTurnstileAsync(string? message = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DenyTurnstileAsync(string? message = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeOrbisApiClient : IOrbisApiClient
    {
        private readonly Func<AccessAttemptRequest, AccessDecision> _handler;

        public FakeOrbisApiClient(Func<AccessAttemptRequest, AccessDecision> handler) => _handler = handler;

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
