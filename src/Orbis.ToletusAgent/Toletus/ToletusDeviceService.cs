using System.Net;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Toletus.LiteNet2;
using Toletus.LiteNet2.Base;
using Toletus.LiteNet2.Command;
using Toletus.LiteNet2.Command.Enums;

namespace Orbis.ToletusAgent.Toletus;

public sealed class ToletusDeviceService : IToletusDeviceService, IDisposable
{
    private const string DefaultReleaseMessage = "Acesso liberado";
    private const string DefaultDenyMessage = "Acesso negado";

    private readonly ILogger<ToletusDeviceService> _logger;
    private readonly IOptionsMonitor<ToletusOptions> _optionsMonitor;

    private ToletusOptions Options => _optionsMonitor.CurrentValue;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private LiteNet2Board? _board;
    private bool _disposed;

    public ToletusDeviceService(
        ILogger<ToletusDeviceService> logger,
        IOptionsMonitor<ToletusOptions> optionsMonitor)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    public event EventHandler<ToletusAccessEvent>? AccessAttemptReceived;

    public bool IsConnected => _board?.Connected == true;

    public string? FirmwareVersion { get; private set; }

    public string? SerialNumber { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(Options.Ip))
        {
            throw new InvalidOperationException("Turnstile IP is not configured.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_board?.Connected == true)
            {
                return;
            }

            var ip = IPAddress.Parse(Options.Ip);
            var serial = string.IsNullOrWhiteSpace(Options.SerialNumber) ? string.Empty : Options.SerialNumber;
            _board = new LiteNet2Board(ip, serial, id: 0);
            AttachHandlers(_board);

            await Task.Run(() => _board.Connect(), cancellationToken).ConfigureAwait(false);

            _board.GetFirmwareVersion();
            if (string.IsNullOrWhiteSpace(serial))
            {
                _board.GetSerialNumber();
            }
            else
            {
                SerialNumber = serial;
            }

            _logger.LogInformation(
                "Connected to Toletus board at {TurnstileIp}. Firmware {FirmwareVersion}, serial {SerialNumber}.",
                Options.Ip,
                FirmwareVersion ?? "pending",
                SerialNumber ?? "pending");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_board is null)
            {
                return;
            }

            DetachHandlers(_board);
            await Task.Run(() => _board.Close(), cancellationToken).ConfigureAwait(false);
            _board = null;

            _logger.LogInformation("Disconnected from Toletus board at {TurnstileIp}.", Options.Ip);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ReleaseTurnstileAsync(string? message = null, CancellationToken cancellationToken = default)
    {
        return ExecuteBoardCommandAsync(
            board => board.ReleaseEntry(message ?? DefaultReleaseMessage),
            cancellationToken);
    }

    public Task DenyTurnstileAsync(string? message = null, CancellationToken cancellationToken = default)
    {
        return ExecuteBoardCommandAsync(
            board => board.TempMessage(message ?? DefaultDenyMessage),
            cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Wait();

        try
        {
            if (_board is not null)
            {
                DetachHandlers(_board);
                _board.Close();
                _board = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task ExecuteBoardCommandAsync(Action<LiteNet2Board> command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_board is null || !_board.Connected)
            {
                throw new InvalidOperationException("Toletus board is not connected.");
            }

            await Task.Run(() => command(_board), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void AttachHandlers(LiteNet2Board board)
    {
        board.OnIdentification += HandleIdentification;
        board.OnConnectionStatusChanged += HandleConnectionStatusChanged;
        board.OnResponse += HandleResponse;
        board.OnStatus += HandleStatus;
    }

    private void DetachHandlers(LiteNet2Board board)
    {
        board.OnIdentification -= HandleIdentification;
        board.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
        board.OnResponse -= HandleResponse;
        board.OnStatus -= HandleStatus;
    }

    private void HandleIdentification(LiteNet2BoardBase _, Identification identification)
    {
        var accessEvent = ToletusAccessEventMapper.Map(identification);
        if (accessEvent is null)
        {
            _logger.LogWarning(
                "Ignored unsupported Toletus identification device {Device} with data {Data}.",
                identification.Device,
                identification.Data);
            return;
        }

        _logger.LogInformation(
            "Toletus access attempt {CredentialType} value {CredentialValue}.",
            accessEvent.CredentialType,
            accessEvent.CredentialValue);

        try
        {
            AccessAttemptReceived?.Invoke(this, accessEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Toletus access event handler failed.");
        }
    }

    private void HandleConnectionStatusChanged(LiteNet2BoardBase board, BoardConnectionStatus status)
    {
        _logger.LogInformation(
            "Toletus connection status {Status} for {TurnstileIp}.",
            status,
            board.Ip);

        if (status != BoardConnectionStatus.Connected || board is not LiteNet2Board liteNetBoard)
        {
            return;
        }

        liteNetBoard.GetFirmwareVersion();
        if (string.IsNullOrWhiteSpace(SerialNumber))
        {
            liteNetBoard.GetSerialNumber();
        }
    }

    private void HandleResponse(LiteNet2Board board, LiteNet2Response response)
    {
        if (response.Command == LiteNet2Commands.GetFirmwareVersion && !string.IsNullOrWhiteSpace(board.FirmwareVersion))
        {
            FirmwareVersion = board.FirmwareVersion;
            _logger.LogInformation("Toletus firmware version {FirmwareVersion}.", FirmwareVersion);
        }

        if (response.Command == LiteNet2Commands.GetSerialNumber && !string.IsNullOrWhiteSpace(board.SerialNumber))
        {
            SerialNumber = board.SerialNumber;
            _logger.LogInformation("Toletus serial number {SerialNumber}.", SerialNumber);
        }
    }

    private void HandleStatus(LiteNet2BoardBase board, string status)
    {
        _logger.LogDebug("Toletus status {Status} for {TurnstileIp}.", status, board.Ip);
    }
}
