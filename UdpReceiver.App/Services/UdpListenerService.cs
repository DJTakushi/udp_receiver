using System.Net;
using System.Net.Sockets;
using UdpReceiver.App.Parsers;

namespace UdpReceiver.App.Services;

public sealed class UdpListenerService : BackgroundService
{
    private readonly ILogger<UdpListenerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MessageStore _messageStore;
    private readonly IReadOnlyList<ICanMessageParser> _parsers;

    public UdpListenerService(
        ILogger<UdpListenerService> logger,
        IConfiguration configuration,
        MessageStore messageStore,
        IEnumerable<ICanMessageParser> parsers)
    {
        _logger = logger;
        _configuration = configuration;
        _messageStore = messageStore;
        _parsers = parsers.ToList();
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

            var parser = _parsers.FirstOrDefault(p => p.CanParse(result.Buffer));
            if (parser is null)
            {
                _logger.LogDebug(
                    "UDP payload on {Endpoint} from {Source} did not match any parser (size={Size}).",
                    localEndpoint,
                    result.RemoteEndPoint,
                    result.Buffer.Length);
                continue;
            }

            var records = parser.Parse(
                data: result.Buffer,
                timestamp: DateTimeOffset.UtcNow,
                source: result.RemoteEndPoint.ToString(),
                target: localEndpoint.ToString());

            if (records.Count == 0)
            {
                _logger.LogDebug(
                    "UDP payload on {Endpoint} from {Source} parsed by {Parser} but produced no CAN frames (size={Size}).",
                    localEndpoint,
                    result.RemoteEndPoint,
                    parser.HardwareType,
                    result.Buffer.Length);
                continue;
            }

            _messageStore.AddRange(records, localEndpoint.Port);
            _logger.LogDebug(
                "UDP payload on {Endpoint} from {Source} parsed by {Parser} into {FrameCount} CAN frame(s).",
                localEndpoint,
                result.RemoteEndPoint,
                parser.HardwareType,
                records.Count);
        }

        _logger.LogInformation("UDP listener stopped on {Endpoint}.", localEndpoint);
    }
}
