using System.Net;
using System.Net.Sockets;
using UdpReceiver.App.Models;

namespace UdpReceiver.App.Services;

public sealed class UdpListenerService : BackgroundService
{
    private readonly ILogger<UdpListenerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MessageStore _messageStore;

    public UdpListenerService(
        ILogger<UdpListenerService> logger,
        IConfiguration configuration,
        MessageStore messageStore)
    {
        _logger = logger;
        _configuration = configuration;
        _messageStore = messageStore;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpointConfigs = _configuration.GetSection("Udp:Endpoints").GetChildren().ToList();

        // Fall back to a single default endpoint when none are configured.
        var endpoints = endpointConfigs.Count > 0
            ? endpointConfigs.Select(ParseEndpoint).ToList()
            : [new IPEndPoint(IPAddress.Any, 5005)];

        var tasks = endpoints.Select(ep => ListenAsync(ep, stoppingToken));
        return Task.WhenAll(tasks);
    }

    private IPEndPoint ParseEndpoint(IConfigurationSection section)
    {
        var port = section.GetValue<int?>("Port") ?? 5005;
        var bindAddressText = section["BindAddress"];

        if (string.IsNullOrWhiteSpace(bindAddressText))
            return new IPEndPoint(IPAddress.Any, port);

        if (!IPAddress.TryParse(bindAddressText, out var address))
        {
            _logger.LogWarning(
                "Invalid BindAddress '{BindAddress}' in configuration, falling back to Any.",
                bindAddressText);
            return new IPEndPoint(IPAddress.Any, port);
        }

        return new IPEndPoint(address, port);
    }

    private async Task ListenAsync(IPEndPoint localEndpoint, CancellationToken stoppingToken)
    {
        using var udpClient = new UdpClient(localEndpoint);
        _logger.LogInformation("UDP listener started on {Endpoint}", localEndpoint);

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udpClient.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while receiving on {Endpoint}.", localEndpoint);
                continue;
            }

            var record = UdpMessageRecord.Parse(
                timestamp: DateTimeOffset.UtcNow,
                source: result.RemoteEndPoint.ToString(),
                target: localEndpoint.ToString(),
                raw: result.Buffer);

            _messageStore.Add(record, localEndpoint.Port);
            _logger.LogDebug("UDP message on {Endpoint} from {Source} | id={Identity} fi={FrameInfo:X2} canId={CanId:X8}",
                localEndpoint, record.Source, record.Identity, record.FrameInfo, record.CanId);
        }

        _logger.LogInformation("UDP listener stopped on {Endpoint}.", localEndpoint);
    }
}
