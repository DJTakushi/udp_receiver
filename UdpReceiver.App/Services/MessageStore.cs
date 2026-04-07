using UdpReceiver.App.Models;
using System.Text;

namespace UdpReceiver.App.Services;

public sealed class MessageStore
{
    private readonly object _gate = new();
    private readonly LinkedList<CanFrameRecord> _messages = new();
    private readonly Dictionary<int, long> _portTotals = new();
    private readonly int _capacity;

    public MessageStore(IConfiguration configuration)
    {
        _capacity = Math.Max(1, configuration.GetValue<int?>("Udp:MaxRecords") ?? 1000);
    }

    public void AddRange(IReadOnlyList<CanFrameRecord> messages, int port)
    {
        if (messages.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            // Preserve parser order
            for (int i = 0; i < messages.Count; i++)
            {
                _messages.AddFirst(messages[i]);
            }

            _portTotals[port] = _portTotals.TryGetValue(port, out var count)
                ? count + messages.Count
                : messages.Count;

            while (_messages.Count > _capacity)
            {
                _messages.RemoveLast();
            }
        }
    }

    public MessageStoreSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new MessageStoreSnapshot(
                Messages: _messages.Take(100).ToList(),
                PortTotals: _portTotals
                    .OrderBy(kvp => kvp.Key)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }
    }

    public string BuildCanLogText()
    {
        lock (_gate)
        {
            var sb = new StringBuilder();

            foreach (var frame in _messages.Reverse())
            {
                var timestampUtc = frame.Timestamp.ToUniversalTime();
                long epochSeconds = timestampUtc.ToUnixTimeSeconds();
                long microseconds = (timestampUtc.Ticks % TimeSpan.TicksPerSecond) / 10;
                string iface = BuildInterfaceName(frame.Target);
                string canId = frame.IsExtended
                    ? frame.CanId.ToString("X8")
                    : (frame.CanId & 0x7FF).ToString("X3");

                sb.Append('(');
                sb.Append(epochSeconds);
                sb.Append('.');
                sb.Append(microseconds.ToString("D6"));
                sb.Append(") ");
                sb.Append(iface);
                sb.Append(' ');
                sb.Append(canId);
                sb.Append('#');

                if (frame.IsRtr)
                {
                    sb.Append('R');
                    sb.Append(frame.CanDlc);
                }
                else
                {
                    int dataLen = Math.Clamp(frame.CanDlc, 0, 8);
                    if (dataLen > 0)
                    {
                        sb.Append(Convert.ToHexString(frame.DataBytes.AsSpan(0, dataLen)));
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
            _portTotals.Clear();
        }
    }

    private static string BuildInterfaceName(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return "can0";
        }

        int separatorIndex = target.LastIndexOf(':');
        if (separatorIndex >= 0 && separatorIndex + 1 < target.Length)
        {
            var portPart = target[(separatorIndex + 1)..];
            if (int.TryParse(portPart, out var port))
            {
                return $"udp{port}";
            }
        }

        return "can0";
    }
}

public sealed record MessageStoreSnapshot(
    IReadOnlyList<CanFrameRecord> Messages,
    IReadOnlyDictionary<int, long> PortTotals);
