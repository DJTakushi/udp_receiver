using UdpReceiver.App.Models;

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
            foreach (var message in messages)
            {
                _messages.AddFirst(message);
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
                Messages: _messages.ToList(),
                PortTotals: _portTotals
                    .OrderBy(kvp => kvp.Key)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }
    }
}

public sealed record MessageStoreSnapshot(
    IReadOnlyList<CanFrameRecord> Messages,
    IReadOnlyDictionary<int, long> PortTotals);
