using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orbis.ToletusAgent;

public sealed class HostLifetimeLogger : IHostedService
{
    private readonly ILogger<HostLifetimeLogger> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public HostLifetimeLogger(ILogger<HostLifetimeLogger> logger, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(() =>
            _logger.LogInformation("Host started."));

        _lifetime.ApplicationStopping.Register(() =>
            _logger.LogInformation("Host stopping."));

        _lifetime.ApplicationStopped.Register(() =>
            _logger.LogInformation("Host stopped."));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
